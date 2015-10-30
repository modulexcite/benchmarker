﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.Logging;

namespace Benchmarks.Euler
{
	public class Euler : Tunnel
	{
		public static ILog logger;

		public static void Main (string[] args, ILog ilog)
		{
			logger = ilog;
			Euler tt = new Euler (1);
			tt.initialise ();
			tt.runiters ();
			tt.validate ();
		}

		public void validate ()
		{
			double[] refval = { 0.0033831416599344965, 0.006812543658280322 };
			double dev = Math.Abs (error - refval [size]);
			if (dev > 1.0e-12) {
				throw new Exception ("Validation failed\nComputed RMS pressure error = " + error + "\nReference value = " + refval [size]);
			}
		}

		private Euler (int sz)
			: base ()
		{
			this.size = sz;
		}
	}

	public class Tunnel
	{
		public int size;
		public int[] datasizes = { 8, 12 };

		public double machff = 0.7;
		/* Inflow mach number */
		public double secondOrderDamping = 1.0;
		public double fourthOrderDamping = 1.0;
		public int ntime = 1;
		/* 0 = local timestep, 1 = time accurate */
		public int scale;
		/* Refine input grid by this factor */
		public double error;

		double[,] a;
		/* Grid cell area */
		double[,] deltat;
		/* Timestep */
		double[,] opg, pg, pg1;
		/* Pressure */
		double[,] sxi, seta;
		double[,] tg, tg1;
		/* Temperature */
		double[,] xnode, ynode;
		/* Storage of node coordinates */

		double[, ,] oldval, newval;
		/* Tepmoray arrays for interpolation */

		double cff, uff, vff, pff, rhoff, tff, jplusff, jminusff;
		/* Far field values */
		int iter = 100;
		/* Number of iterations */
		int imax, jmax;
		/* Number of nodes in x and y direction*/
		int imaxin, jmaxin;
		/* Number of nodes in x and y direction in unscaled data */
		int nf = 6;
		/* Number of fields in data file */
		Statevector[,] d;
		/* Damping coefficients */
		Statevector[,] f, g;
		/* Flux Vectors */
		Statevector[,] r, ug1;
		Statevector[,] ug;
		/* Storage of data */

		const double Cp = 1004.5;
		/* specific heat, const pres. */
		const double Cv = 717.5;
		/* specific heat, const vol. */
		const double gamma = 1.4;
		/* Ratio of specific heats */
		const double rgas = 287.0;
		/* Gas Constant */
		const double fourthOrderNormalizer = 0.02;
		/* Damping coefficients */
		const double secondOrderNormalizer = 0.02;

		public void initialise ()
		{
			int i, j, k;             /* Dummy counters */
			double scrap, scrap2;     /* Temporary storage */

			/* Set scale factor for interpolation */
			scale = datasizes [size];

			/* Open data file */
			String instr = Constants.INPUT.Trim ();
			char[] spt = new char[4];
			spt [0] = ' ';
			spt [1] = '\n';
			spt [2] = '\t';
			spt [3] = '\r';
			String[] intokenstmp = instr.Split (spt);

			List<String> intokens = new List<String> ();
			foreach (String ss in intokenstmp) {
				if (!String.IsNullOrEmpty (ss))
					intokens.Add (ss);
			}

			//we just assume the file is good and go for it
			imaxin = Int32.Parse (intokens [0]);
			jmaxin = Int32.Parse (intokens [1]);
			int pfloc = 2;
			
			// Read data into temporary array 
			// note: dummy extra row and column needed to make interpolation simple
			oldval = new double[nf, imaxin + 1, jmaxin + 1];

			for (i = 0; i < imaxin; i++) {
				for (j = 0; j < jmaxin; j++) {
					for (k = 0; k < nf; k++) {
						oldval [k, i, j] = Double.Parse (intokens [pfloc]);
						++pfloc;
					}
				}
			}

			//interpolate onto finer grid 
			imax = (imaxin - 1) * scale + 1;
			jmax = (jmaxin - 1) * scale + 1;
			newval = new double[nf, imax, jmax];

			for (k = 0; k < nf; k++) {
				for (i = 0; i < imax; i++) {
					for (j = 0; j < jmax; j++) {
						int iold = i / scale;
						int jold = j / scale;
						double xf = ((double)i % scale) / ((double)scale);
						double yf = ((double)j % scale) / ((double)scale);
						newval [k, i, j] = (1.0 - xf) * (1.0 - yf) * oldval [k, iold, jold] + (1.0 - xf) * yf * oldval [k, iold, jold + 1] + xf * (1.0 - yf) * oldval [k, iold + 1, jold] + xf * yf * oldval [k, iold + 1, jold + 1];
					}
				}
			}

			//create arrays 
			deltat = new double[imax + 1, jmax + 2];
			opg = new double[imax + 2, jmax + 2];
			pg = new double[imax + 2, jmax + 2];
			pg1 = new double[imax + 2, jmax + 2];
			sxi = new double[imax + 2, jmax + 2];
			seta = new double[imax + 2, jmax + 2];
			tg = new double[imax + 2, jmax + 2];
			tg1 = new double[imax + 2, jmax + 2];
			ug = new Statevector[imax + 2, jmax + 2];
			a = new double[imax, jmax];
			d = new Statevector[imax + 2, jmax + 2];
			f = new Statevector[imax + 2, jmax + 2];
			g = new Statevector[imax + 2, jmax + 2];
			r = new Statevector[imax + 2, jmax + 2];
			ug1 = new Statevector[imax + 2, jmax + 2];
			xnode = new double[imax, jmax];
			ynode = new double[imax, jmax];

			for (i = 0; i < imax + 2; ++i) {
				for (j = 0; j < jmax + 2; ++j) {
					d [i, j] = new Statevector ();
					f [i, j] = new Statevector ();
					g [i, j] = new Statevector ();
					r [i, j] = new Statevector ();
					ug [i, j] = new Statevector ();
					ug1 [i, j] = new Statevector ();
				}
			}

			/* Set farfield values (we use normalized units for everything */
			cff = 1.0;
			vff = 0.0;
			pff = 1.0 / gamma;
			rhoff = 1.0;
			tff = pff / (rhoff * rgas);

			// Copy the interpolated data to arrays 
			for (i = 0; i < imax; i++) {
				for (j = 0; j < jmax; j++) {
					xnode [i, j] = newval [0, i, j];
					ynode [i, j] = newval [1, i, j];
					ug [i + 1, j + 1].a = newval [2, i, j];
					ug [i + 1, j + 1].b = newval [3, i, j];
					ug [i + 1, j + 1].c = newval [4, i, j];
					ug [i + 1, j + 1].d = newval [5, i, j];

					scrap = ug [i + 1, j + 1].c / ug [i + 1, j + 1].a;
					scrap2 = ug [i + 1, j + 1].b / ug [i + 1, j + 1].a;
					tg [i + 1, j + 1] = ug [i + 1, j + 1].d / ug [i + 1, j + 1].a - (0.5 * (scrap * scrap + scrap2 * scrap2));
					tg [i + 1, j + 1] = tg [i + 1, j + 1] / Cv;
					pg [i + 1, j + 1] = rgas * ug [i + 1, j + 1].a * tg [i + 1, j + 1];
				}
			}


			/* Calculate grid cell areas */
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j)
					a [i, j] = 0.5 * ((xnode [i, j] - xnode [i - 1, j - 1]) * (ynode [i - 1, j] - ynode [i, j - 1]) - (ynode [i, j] - ynode [i - 1, j - 1]) * (xnode [i - 1, j] - xnode [i, j - 1]));
			}
			// throw away temporary arrays 
			oldval = newval = null;
		}


		void doIteration ()
		{
			double scrap;
			int i, j;

			/* Record the old pressure values */
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					opg [i, j] = pg [i, j];
				}
			}

			calculateDummyCells (pg, tg, ug);
			calculateDeltaT ();

			calculateDamping (pg, ug);

			/* Do the integration */
			/* Step 1 */
			calculateF (pg, tg, ug);
			calculateG (pg, tg, ug);
			calculateR ();

			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					ug1 [i, j].a = ug [i, j].a - 0.25 * deltat [i, j] / a [i, j] * (r [i, j].a - d [i, j].a);
					ug1 [i, j].b = ug [i, j].b - 0.25 * deltat [i, j] / a [i, j] * (r [i, j].b - d [i, j].b);
					ug1 [i, j].c = ug [i, j].c - 0.25 * deltat [i, j] / a [i, j] * (r [i, j].c - d [i, j].c);
					ug1 [i, j].d = ug [i, j].d - 0.25 * deltat [i, j] / a [i, j] * (r [i, j].d - d [i, j].d);
				}
			}
			calculateStateVar (pg1, tg1, ug1);

			/* Step 2 */
			calculateDummyCells (pg1, tg1, ug1);
			calculateF (pg1, tg1, ug1);
			calculateG (pg1, tg1, ug1);
			calculateR ();
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					ug1 [i, j].a = ug [i, j].a - 0.33333 * deltat [i, j] / a [i, j] * (r [i, j].a - d [i, j].a);
					ug1 [i, j].b = ug [i, j].b - 0.33333 * deltat [i, j] / a [i, j] * (r [i, j].b - d [i, j].b);
					ug1 [i, j].c = ug [i, j].c - 0.33333 * deltat [i, j] / a [i, j] * (r [i, j].c - d [i, j].c);
					ug1 [i, j].d = ug [i, j].d - 0.33333 * deltat [i, j] / a [i, j] * (r [i, j].d - d [i, j].d);
				}
			}
			calculateStateVar (pg1, tg1, ug1);

			/* Step 3 */
			calculateDummyCells (pg1, tg1, ug1);
			calculateF (pg1, tg1, ug1);
			calculateG (pg1, tg1, ug1);
			calculateR ();
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					ug1 [i, j].a = ug [i, j].a - 0.5 * deltat [i, j] / a [i, j] * (r [i, j].a - d [i, j].a);
					ug1 [i, j].b = ug [i, j].b - 0.5 * deltat [i, j] / a [i, j] * (r [i, j].b - d [i, j].b);
					ug1 [i, j].c = ug [i, j].c - 0.5 * deltat [i, j] / a [i, j] * (r [i, j].c - d [i, j].c);
					ug1 [i, j].d = ug [i, j].d - 0.5 * deltat [i, j] / a [i, j] * (r [i, j].d - d [i, j].d);
				}
			}
			calculateStateVar (pg1, tg1, ug1);

			/* Step 4 (final step) */
			calculateDummyCells (pg1, tg1, ug1);
			calculateF (pg1, tg1, ug1);
			calculateG (pg1, tg1, ug1);
			calculateR ();
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					ug [i, j].a -= deltat [i, j] / a [i, j] * (r [i, j].a - d [i, j].a);
					ug [i, j].b -= deltat [i, j] / a [i, j] * (r [i, j].b - d [i, j].b);
					ug [i, j].c -= deltat [i, j] / a [i, j] * (r [i, j].c - d [i, j].c);
					ug [i, j].d -= deltat [i, j] / a [i, j] * (r [i, j].d - d [i, j].d);
				}
			}
			calculateStateVar (pg, tg, ug);

			/* calculate RMS Pressure Error */
			error = 0.0;
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					scrap = pg [i, j] - opg [i, j];
					error += scrap * scrap;
				}
			}
			error = Math.Sqrt (error / (double)((imax - 1) * (jmax - 1)));
		}

		/* Calculates the new state values for range-kutta */
		private void calculateStateVar (double[,] localpg, double[,] localtg, Statevector[,] localug)
		{
			double temp, temp2;
			int i, j;

			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					temp = localug [i, j].b;
					temp2 = localug [i, j].c;
					localtg [i, j] = localug [i, j].d / localug [i, j].a - 0.5 * (temp * temp + temp2 * temp2) / (localug [i, j].a * localug [i, j].a);

					localtg [i, j] = localtg [i, j] / Cv;
					localpg [i, j] = localug [i, j].a * rgas * localtg [i, j];
				}
			}
		}

		private void calculateR ()
		{

			double deltax, deltay;
			double temp;
			int i, j;
			
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {

					/* Start by clearing R */
					r [i, j].a = 0.0;
					r [i, j].b = 0.0;
					r [i, j].c = 0.0;
					r [i, j].d = 0.0;

					/* East Face */
					deltay = (ynode [i, j] - ynode [i, j - 1]);
					deltax = (xnode [i, j] - xnode [i, j - 1]);
					temp = 0.5 * deltay;
					r [i, j].a += temp * (f [i, j].a + f [i + 1, j].a);
					r [i, j].b += temp * (f [i, j].b + f [i + 1, j].b);
					r [i, j].c += temp * (f [i, j].c + f [i + 1, j].c);
					r [i, j].d += temp * (f [i, j].d + f [i + 1, j].d);

					temp = -0.5 * deltax;
					r [i, j].a += temp * (g [i, j].a + g [i + 1, j].a);
					r [i, j].b += temp * (g [i, j].b + g [i + 1, j].b);
					r [i, j].c += temp * (g [i, j].c + g [i + 1, j].c);
					r [i, j].d += temp * (g [i, j].d + g [i + 1, j].d);

					/* South Face */
					deltay = (ynode [i, j - 1] - ynode [i - 1, j - 1]);
					deltax = (xnode [i, j - 1] - xnode [i - 1, j - 1]);

					temp = 0.5 * deltay;
					r [i, j].a += temp * (f [i, j].a + f [i, j - 1].a);
					r [i, j].b += temp * (f [i, j].b + f [i, j - 1].b);
					r [i, j].c += temp * (f [i, j].c + f [i, j - 1].c);
					r [i, j].d += temp * (f [i, j].d + f [i, j - 1].d);

					temp = -0.5 * deltax;
					r [i, j].a += temp * (g [i, j].a + g [i, j - 1].a);
					r [i, j].b += temp * (g [i, j].b + g [i, j - 1].b);
					r [i, j].c += temp * (g [i, j].c + g [i, j - 1].c);
					r [i, j].d += temp * (g [i, j].d + g [i, j - 1].d);

					/* West Face */
					deltay = (ynode [i - 1, j - 1] - ynode [i - 1, j]);
					deltax = (xnode [i - 1, j - 1] - xnode [i - 1, j]);

					temp = 0.5 * deltay;
					r [i, j].a += temp * (f [i, j].a + f [i - 1, j].a);
					r [i, j].b += temp * (f [i, j].b + f [i - 1, j].b);
					r [i, j].c += temp * (f [i, j].c + f [i - 1, j].c);
					r [i, j].d += temp * (f [i, j].d + f [i - 1, j].d);

					temp = -0.5 * deltax;
					r [i, j].a += temp * (g [i, j].a + g [i - 1, j].a);
					r [i, j].b += temp * (g [i, j].b + g [i - 1, j].b);
					r [i, j].c += temp * (g [i, j].c + g [i - 1, j].c);
					r [i, j].d += temp * (g [i, j].d + g [i - 1, j].d);


					/* North Face */
					deltay = (ynode [i - 1, j] - ynode [i, j]);
					deltax = (xnode [i - 1, j] - xnode [i, j]);

					temp = 0.5 * deltay;
					r [i, j].a += temp * (f [i, j].a + f [i + 1, j].a);
					r [i, j].b += temp * (f [i, j].b + f [i + 1, j].b);
					r [i, j].c += temp * (f [i, j].c + f [i + 1, j].c);
					r [i, j].d += temp * (f [i, j].d + f [i + 1, j].d);

					temp = -0.5 * deltax;
					r [i, j].a += temp * (g [i, j].a + g [i, j + 1].a);
					r [i, j].b += temp * (g [i, j].b + g [i, j + 1].b);
					r [i, j].c += temp * (g [i, j].c + g [i, j + 1].c);
					r [i, j].d += temp * (g [i, j].d + g [i, j + 1].d);
				}
			}
		}

		private void calculateG (double[,] localpg, double[,] localtg, Statevector[,] localug)
		{
			double temp, temp2, temp3;
			double v;
			int i, j;

			for (i = 0; i < imax + 1; ++i) {
				for (j = 0; j < jmax + 1; ++j) {
					v = localug [i, j].c / localug [i, j].a;
					g [i, j].a = localug [i, j].c;
					g [i, j].b = localug [i, j].b * v;
					g [i, j].c = localug [i, j].c * v + localpg [i, j];
					temp = localug [i, j].b * localug [i, j].b;
					temp2 = localug [i, j].c * localug [i, j].c;
					temp3 = localug [i, j].a * localug [i, j].a;
					g [i, j].d = localug [i, j].c * (Cp * localtg [i, j] + (0.5 * (temp + temp2) / (temp3)));
				}
			}
		}


		private void calculateF (double[,] localpg, double[,] localtg, Statevector[,] localug)
		{
			{
				double u;
				double temp1, temp2, temp3;
				int i, j;

				for (i = 0; i < imax + 1; ++i) {
					for (j = 0; j < jmax + 1; ++j) {
						u = localug [i, j].b / localug [i, j].a;
						f [i, j].a = localug [i, j].b;
						f [i, j].b = localug [i, j].b * u + localpg [i, j];
						f [i, j].c = localug [i, j].c * u;
						temp1 = localug [i, j].b * localug [i, j].b;
						temp2 = localug [i, j].c * localug [i, j].c;
						temp3 = localug [i, j].a * localug [i, j].a;
						f [i, j].d = localug [i, j].b * (Cp * localtg [i, j] + (0.5 * (temp1 + temp2) / (temp3)));
					}
				}
			}
		}

		private void calculateDamping (double[,] localpg, Statevector[,] localug)
		{
			double adt, sbar;
			double nu2;
			double nu4;
			double tempdouble;
			int i, j;
			Statevector temp = new Statevector ();
			Statevector temp2 = new Statevector ();
			Statevector scrap2 = new Statevector ();
			Statevector scrap4 = new Statevector ();

			nu2 = secondOrderDamping * secondOrderNormalizer;
			nu4 = fourthOrderDamping * fourthOrderNormalizer;

			/* First do the pressure switches */
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					sxi [i, j] = Math.Abs (localpg [i + 1, j] - 2.0 * localpg [i, j] + localpg [i - 1, j]) / localpg [i, j];
					seta [i, j] = Math.Abs (localpg [i, j + 1] - 2.0 * localpg [i, j] + localpg [i, j - 1]) / localpg [i, j];
				}
			}

			/* Then calculate the fluxes */
			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {

					/* Clear values */
					/* East Face */
					if (i > 1 && i < imax - 1) {
						adt = (a [i, j] + a [i + 1, j]) / (deltat [i, j] + deltat [i + 1, j]);
						sbar = (sxi [i + 1, j] + sxi [i, j]) * 0.5;
					} else {
						adt = a [i, j] / deltat [i, j];
						sbar = sxi [i, j];
					}
					tempdouble = nu2 * sbar * adt;
					scrap2.a = tempdouble * (localug [i + 1, j].a - localug [i, j].a);
					scrap2.b = tempdouble * (localug [i + 1, j].b - localug [i, j].b);
					scrap2.c = tempdouble * (localug [i + 1, j].c - localug [i, j].c);
					scrap2.d = tempdouble * (localug [i + 1, j].d - localug [i, j].d);

					if (i > 1 && i < imax - 1) {
						temp = localug [i + 2, j].svect (localug [i - 1, j]);

						temp2.a = 3.0 * (localug [i, j].a - localug [i + 1, j].a);
						temp2.b = 3.0 * (localug [i, j].b - localug [i + 1, j].b);
						temp2.c = 3.0 * (localug [i, j].c - localug [i + 1, j].c);
						temp2.d = 3.0 * (localug [i, j].d - localug [i + 1, j].d);

						tempdouble = -nu4 * adt;
						scrap4.a = tempdouble * (temp.a + temp2.a);
						scrap4.b = tempdouble * (temp.a + temp2.b);
						scrap4.c = tempdouble * (temp.a + temp2.c);
						scrap4.d = tempdouble * (temp.a + temp2.d);
					} else {
						scrap4.a = 0.0;
						scrap4.b = 0.0;
						scrap4.c = 0.0;
						scrap4.d = 0.0;
					}

					temp.a = scrap2.a + scrap4.a;
					temp.b = scrap2.b + scrap4.b;
					temp.c = scrap2.c + scrap4.c;
					temp.d = scrap2.d + scrap4.d;
					d [i, j] = temp;

					/* West Face */
					if (i > 1 && i < imax - 1) {
						adt = (a [i, j] + a [i - 1, j]) / (deltat [i, j] + deltat [i - 1, j]);
						sbar = (sxi [i, j] + sxi [i - 1, j]) * 0.5;
					} else {
						adt = a [i, j] / deltat [i, j];
						sbar = sxi [i, j];
					}

					tempdouble = -nu2 * sbar * adt;
					scrap2.a = tempdouble * (localug [i, j].a - localug [i - 1, j].a);
					scrap2.b = tempdouble * (localug [i, j].b - localug [i - 1, j].b);
					scrap2.c = tempdouble * (localug [i, j].c - localug [i - 1, j].c);
					scrap2.d = tempdouble * (localug [i, j].d - localug [i - 1, j].d);


					if (i > 1 && i < imax - 1) {
						temp = localug [i + 1, j].svect (localug [i - 2, j]);
						temp2.a = 3.0 * (localug [i - 1, j].a - localug [i, j].a);
						temp2.b = 3.0 * (localug [i - 1, j].b - localug [i, j].b);
						temp2.c = 3.0 * (localug [i - 1, j].c - localug [i, j].c);
						temp2.d = 3.0 * (localug [i - 1, j].d - localug [i, j].d);

						tempdouble = nu4 * adt;
						scrap4.a = tempdouble * (temp.a + temp2.a);
						scrap4.b = tempdouble * (temp.a + temp2.b);
						scrap4.c = tempdouble * (temp.a + temp2.c);
						scrap4.d = tempdouble * (temp.a + temp2.d);
					} else {
						scrap4.a = 0.0;
						scrap4.b = 0.0;
						scrap4.c = 0.0;
						scrap4.d = 0.0;
					}

					d [i, j].a += scrap2.a + scrap4.a;
					d [i, j].b += scrap2.b + scrap4.b;
					d [i, j].c += scrap2.c + scrap4.c;
					d [i, j].d += scrap2.d + scrap4.d;

					/* North Face */
					if (j > 1 && j < jmax - 1) {
						adt = (a [i, j] + a [i, j + 1]) / (deltat [i, j] + deltat [i, j + 1]);
						sbar = (seta [i, j] + seta [i, j + 1]) * 0.5;
					} else {
						adt = a [i, j] / deltat [i, j];
						sbar = seta [i, j];
					}
					tempdouble = nu2 * sbar * adt;
					scrap2.a = tempdouble * (localug [i, j + 1].a - localug [i, j].a);
					scrap2.b = tempdouble * (localug [i, j + 1].b - localug [i, j].b);
					scrap2.c = tempdouble * (localug [i, j + 1].c - localug [i, j].c);
					scrap2.d = tempdouble * (localug [i, j + 1].d - localug [i, j].d);

					if (j > 1 && j < jmax - 1) {
						temp = localug [i, j + 2].svect (localug [i, j - 1]);
						temp2.a = 3.0 * (localug [i, j].a - localug [i, j + 1].a);
						temp2.b = 3.0 * (localug [i, j].b - localug [i, j + 1].b);
						temp2.c = 3.0 * (localug [i, j].c - localug [i, j + 1].c);
						temp2.d = 3.0 * (localug [i, j].d - localug [i, j + 1].d);

						tempdouble = -nu4 * adt;
						scrap4.a = tempdouble * (temp.a + temp2.a);
						scrap4.b = tempdouble * (temp.a + temp2.b);
						scrap4.c = tempdouble * (temp.a + temp2.c);
						scrap4.d = tempdouble * (temp.a + temp2.d);
					} else {
						scrap4.a = 0.0;
						scrap4.b = 0.0;
						scrap4.c = 0.0;
						scrap4.d = 0.0;
					}
					d [i, j].a += scrap2.a + scrap4.a;
					d [i, j].b += scrap2.b + scrap4.b;
					d [i, j].c += scrap2.c + scrap4.c;
					d [i, j].d += scrap2.d + scrap4.d;

					/* South Face */
					if (j > 1 && j < jmax - 1) {
						adt = (a [i, j] + a [i, j - 1]) / (deltat [i, j] + deltat [i, j - 1]);
						sbar = (seta [i, j] + seta [i, j - 1]) * 0.5;
					} else {
						adt = a [i, j] / deltat [i, j];
						sbar = seta [i, j];
					}
					tempdouble = -nu2 * sbar * adt;
					scrap2.a = tempdouble * (localug [i, j].a - localug [i, j - 1].a);
					scrap2.b = tempdouble * (localug [i, j].b - localug [i, j - 1].b);
					scrap2.c = tempdouble * (localug [i, j].c - localug [i, j - 1].c);
					scrap2.d = tempdouble * (localug [i, j].d - localug [i, j - 1].d);

					if (j > 1 && j < jmax - 1) {
						temp = localug [i, j + 1].svect (localug [i, j - 2]);
						temp2.a = 3.0 * (localug [i, j - 1].a - localug [i, j].a);
						temp2.b = 3.0 * (localug [i, j - 1].b - localug [i, j].b);
						temp2.c = 3.0 * (localug [i, j - 1].c - localug [i, j].c);
						temp2.d = 3.0 * (localug [i, j - 1].d - localug [i, j].d);

						tempdouble = nu4 * adt;
						scrap4.a = tempdouble * (temp.a + temp2.a);
						scrap4.b = tempdouble * (temp.a + temp2.b);
						scrap4.c = tempdouble * (temp.a + temp2.c);
						scrap4.d = tempdouble * (temp.a + temp2.d);
					} else {
						scrap4.a = 0.0;
						scrap4.b = 0.0;
						scrap4.c = 0.0;
						scrap4.d = 0.0;
					}
					d [i, j].a += scrap2.a + scrap4.a;
					d [i, j].b += scrap2.b + scrap4.b;
					d [i, j].c += scrap2.c + scrap4.c;
					d [i, j].d += scrap2.d + scrap4.d;
				}
			}
		}

		private void calculateDeltaT ()
		{
			double xeta, yeta, xxi, yxi;              /* Local change in x and y */
			int i, j;
			double mint;
			double c, q, r;
			double safety_factor = 0.7;

			for (i = 1; i < imax; ++i) {
				for (j = 1; j < jmax; ++j) {
					xxi = (xnode [i, j] - xnode [i - 1, j] + xnode [i, j - 1] - xnode [i - 1, j - 1]) * 0.5;
					yxi = (ynode [i, j] - ynode [i - 1, j] + ynode [i, j - 1] - ynode [i - 1, j - 1]) * 0.5;
					xeta = (xnode [i, j] - xnode [i, j - 1] + xnode [i - 1, j] - xnode [i - 1, j - 1]) * 0.5;
					yeta = (ynode [i, j] - ynode [i, j - 1] + ynode [i - 1, j] - ynode [i - 1, j - 1]) * 0.5;

					q = (yeta * ug [i, j].b - xeta * ug [i, j].c);
					r = (-yxi * ug [i, j].b + xxi * ug [i, j].c);
					c = Math.Sqrt (gamma * rgas * tg [i, j]);

					deltat [i, j] = safety_factor * 2.8284 * a [i, j] / ((Math.Abs (q) + Math.Abs (r)) / ug [i, j].a + c * Math.Sqrt (xxi * xxi + yxi * yxi + xeta * xeta + yeta * yeta + 2.0 * Math.Abs (xeta * xxi + yeta * yxi)));
				}
			}

			/* If that's the user's choice, make it time accurate */
			if (ntime == 1) {
				mint = 100000.0;
				for (i = 1; i < imax; ++i) {
					for (j = 1; j < jmax; ++j) {
						if (deltat [i, j] < mint)
							mint = deltat [i, j];
					}
				}

				for (i = 1; i < imax; ++i) {
					for (j = 1; j < jmax; ++j)
						deltat [i, j] = mint;
				}
			}
		}

		private void calculateDummyCells (double[,] localpg, double[,] localtg, Statevector[,] localug)
		{
			double c;
			double jminus;
			double jplus;
			double s;
			double rho, temp, u, v;
			double scrap, scrap2;
			double theta;
			double uprime;
			int i, j;
			Vector2 norm = new Vector2 ();
			Vector2 tan = new Vector2 ();
			Vector2 u1 = new Vector2 ();

			uff = machff;
			jplusff = uff + 2.0 / (gamma - 1.0) * cff;
			jminusff = uff - 2.0 / (gamma - 1.0) * cff;

			for (i = 1; i < imax; ++i) {
				/* Bottom wall boundary cells */
				/* Routine checked by brute force for initial conditions, 4/9; 4:30 */
				/* Routine checked by brute force for random conditions, 4/13, 4:40 pm */
				/* Construct tangent vectors */
				tan.ihat = xnode [i, 0] - xnode [i - 1, 0];
				tan.jhat = ynode [i, 0] - ynode [i - 1, 0];
				norm.ihat = -(ynode [i, 0] - ynode [i - 1, 0]);
				norm.jhat = xnode [i, 0] - xnode [i - 1, 0];

				scrap = tan.magnitude ();
				tan.ihat = tan.ihat / scrap;
				tan.jhat = tan.jhat / scrap;
				scrap = norm.magnitude ();
				norm.ihat = norm.ihat / scrap;
				norm.jhat = norm.jhat / scrap;

				/* now set some state variables */
				rho = localug [i, 1].a;
				localtg [i, 0] = localtg [i, 1];
				u1.ihat = localug [i, 1].b / rho;
				u1.jhat = localug [i, 1].c / rho;

				u = u1.dot (tan) + u1.dot (norm) * tan.jhat / norm.jhat;
				u = u / (tan.ihat - (norm.ihat * tan.jhat / norm.jhat));

				v = -(u1.dot (norm) + u * norm.ihat) / norm.jhat;

				/* And construct the new state vector */
				localug [i, 0].a = localug [i, 1].a;
				localug [i, 0].b = rho * u;
				localug [i, 0].c = rho * v;
				localug [i, 0].d = rho * (Cv * localtg [i, 0] + 0.5 * (u * u + v * v));
				localpg [i, 0] = localpg [i, 1];

				/* Top Wall Boundary Cells */
				/* Checked numerically for default conditions, 4/9 at 5:30 pm */
				/* Construct normal and tangent vectors */
				/* This part checked and works; it produces the correct vectors */
				tan.ihat = xnode [i, jmax - 1] - xnode [i - 1, jmax - 1];
				tan.jhat = ynode [i, jmax - 1] - ynode [i - 1, jmax - 1];
				norm.ihat = ynode [i, jmax - 1] - ynode [i - 1, jmax - 1];
				norm.jhat = -(xnode [i, jmax - 1] - xnode [i - 1, jmax - 1]);

				scrap = tan.magnitude ();
				tan.ihat = tan.ihat / scrap;
				tan.jhat = tan.jhat / scrap;
				scrap = norm.magnitude ();
				norm.ihat = norm.ihat / scrap;
				norm.jhat = norm.jhat / scrap;

				/* now set some state variables */
				rho = localug [i, jmax - 1].a;
				temp = localtg [i, jmax - 1];
				u1.ihat = localug [i, jmax - 1].b / rho;
				u1.jhat = localug [i, jmax - 1].c / rho;

				u = u1.dot (tan) + u1.dot (norm) * tan.jhat / norm.jhat;
				u = u / (tan.ihat - (norm.ihat * tan.jhat / norm.jhat));

				v = -(u1.dot (norm) + u * norm.ihat) / norm.jhat;

				/* And construct the new state vector */
				localug [i, jmax].a = localug [i, jmax - 1].a;
				localug [i, jmax].b = rho * u;
				localug [i, jmax].c = rho * v;
				localug [i, jmax].d = rho * (Cv * temp + 0.5 * (u * u + v * v));
				localtg [i, jmax] = temp;
				localpg [i, jmax] = localpg [i, jmax - 1];
			}

			for (j = 1; j < jmax; ++j) {
				/* Inlet Boundary Cells: unchecked */
				/* Construct the normal vector; This works, 4/10, 2:00 pm */
				norm.ihat = ynode [0, j - 1] - ynode [0, j];
				norm.jhat = xnode [0, j] - xnode [0, j - 1];
				scrap = norm.magnitude ();
				norm.ihat = norm.ihat / scrap;
				norm.jhat = norm.jhat / scrap;
				theta = Math.Acos ((ynode [0, j - 1] - ynode [0, j]) /
				Math.Sqrt ((xnode [0, j] - xnode [0, j - 1]) * (xnode [0, j] - xnode [0, j - 1]) + (ynode [0, j - 1] - ynode [0, j]) * (ynode [0, j - 1] - ynode [0, j])));

				u1.ihat = localug [1, j].b / localug [1, j].a;
				u1.jhat = localug [1, j].c / localug [1, j].a;
				uprime = u1.ihat * Math.Cos (theta);
				c = Math.Sqrt (gamma * rgas * localtg [1, j]);
				/* Supersonic inflow; works on the initial cond, 4/10 at 3:10 pm */
				if (uprime < -c) {
					/* Use far field conditions */
					localug [0, j].a = rhoff;
					localug [0, j].b = rhoff * uff;
					localug [0, j].c = rhoff * vff;
					localug [0, j].d = rhoff * (Cv * tff + 0.5 * (uff * uff + vff * vff));
					localtg [0, j] = tff;
					localpg [0, j] = pff;
				}
				/* Subsonic inflow */
				/* This works on the initial conditions 4/10 @ 2:20 pm */
				else if (uprime < 0.0) {
					/* Calculate Riemann invarients here */
					jminus = u1.ihat - 2.0 / (gamma - 1.0) * c;
					s = Math.Log (pff) - gamma * Math.Log (rhoff);
					v = vff;

					u = (jplusff + jminus) / 2.0;
					scrap = (jplusff - u) * (gamma - 1.0) * 0.5;
					localtg [0, j] = (1.0 / (gamma * rgas)) * scrap * scrap;
					localpg [0, j] = Math.Exp (s) / Math.Pow ((rgas * localtg [0, j]), gamma);
					localpg [0, j] = Math.Pow (localpg [0, j], 1.0 / (1.0 - gamma));

					/* And now: construct the new state vector */
					localug [0, j].a = localpg [0, j] / (rgas * localtg [0, j]);
					localug [0, j].b = localug [0, j].a * u;
					localug [0, j].c = localug [0, j].a * v;
					localug [0, j].d = localug [0, j].a * (Cv * tff + 0.5 * (u * u + v * v));
				}
				/* Other options */
				/* We should throw an exception here */
				else {
					throw new Exception ("You have outflow at the inlet, which is not allowed.");
				}

				/* Outlet Boundary Cells */
				/* Construct the normal vector; works, 4/10 3:10 pm */
				norm.ihat = ynode [0, j] - ynode [0, j - 1];
				norm.jhat = xnode [0, j - 1] - xnode [0, j];
				scrap = norm.magnitude ();
				norm.ihat = norm.ihat / scrap;
				norm.jhat = norm.jhat / scrap;
				scrap = xnode [0, j - 1] - xnode [0, j];
				scrap2 = ynode [0, j] - ynode [0, j - 1];
				theta = Math.Acos ((ynode [0, j] - ynode [0, j - 1]) / Math.Sqrt (scrap * scrap + scrap2 * scrap2));

				u1.ihat = localug [imax - 1, j].b / localug [imax - 1, j].a;
				u1.jhat = localug [imax - 1, j].c / localug [imax - 1, j].a;
				uprime = u1.ihat * Math.Cos (theta);
				c = Math.Sqrt (gamma * rgas * localtg [imax - 1, j]);
				/* Supersonic outflow; works for defaults cond, 4/10: 3:10 pm */
				if (uprime > c) {
					/* Use a backward difference 2nd order derivative approximation */
					/* To set values at exit */
					localug [imax, j].a = 2.0 * localug [imax - 1, j].a - localug [imax - 2, j].a;
					localug [imax, j].b = 2.0 * localug [imax - 1, j].b - localug [imax - 2, j].b;
					localug [imax, j].c = 2.0 * localug [imax - 1, j].c - localug [imax - 2, j].c;
					localug [imax, j].d = 2.0 * localug [imax - 1, j].d - localug [imax - 2, j].d;
					localpg [imax, j] = 2.0 * localpg [imax - 1, j] - localpg [imax - 2, j];
					localtg [imax, j] = 2.0 * localtg [imax - 1, j] - localtg [imax - 2, j];
				}
				/* Subsonic Outflow; works for defaults cond, 4/10: 3:10 pm */
				else if (uprime < c && uprime > 0) {
					jplus = u1.ihat + 2.0 / (gamma - 1) * c;
					v = localug [imax - 1, j].c / localug [imax - 1, j].a;
					s = Math.Log (localpg [imax - 1, j]) - gamma * Math.Log (localug [imax - 1, j].a);

					u = (jplus + jminusff) / 2.0;
					scrap = (jplus - u) * (gamma - 1.0) * 0.5;
					localtg [imax, j] = (1.0 / (gamma * rgas)) * scrap * scrap;
					localpg [imax, j] = Math.Exp (s) / Math.Pow ((rgas * localtg [imax, j]), gamma);
					localpg [imax, j] = Math.Pow (localpg [imax, j], 1.0 / (1.0 - gamma));
					rho = localpg [imax, j] / (rgas * localtg [imax, j]);

					/* And now, construct the new state vector */
					localug [imax, j].a = rho;
					localug [imax, j].b = rho * u;
					localug [imax, j].c = rho * v;
					localug [imax, j].d = rho * (Cv * localtg [imax, j] + 0.5 * (u * u + v * v));

				}
				/* Other cases that shouldn't have to be used. */
				else if (uprime < -c) {
					/* Supersonic inflow */
					/* Use far field conditions */
					localug [0, j].a = rhoff;
					localug [0, j].b = rhoff * uff;
					localug [0, j].c = rhoff * vff;
					localug [0, j].d = rhoff * (Cv * tff + 0.5 * (uff * uff + vff * vff));
					localtg [0, j] = tff;
					localpg [0, j] = pff;
				}
				/* Subsonic inflow */
				/* This works on the initial conditions 4/10 @ 2:20 pm */
				else if (uprime < 0.0) {
					/* Debug: throw exception here? */
					/* Calculate Riemann invarients here */
					jminus = u1.ihat - 2.0 / (gamma - 1.0) * c;
					s = Math.Log (pff) - gamma * Math.Log (rhoff);
					v = vff;

					u = (jplusff + jminus) / 2.0;
					scrap = (jplusff - u) * (gamma - 1.0) * 0.5;
					localtg [0, j] = (1.0 / (gamma * rgas)) * scrap * scrap;
					localpg [0, j] = Math.Exp (s) / Math.Pow ((rgas * localtg [0, j]), gamma);
					localpg [0, j] = Math.Pow (localpg [0, j], 1.0 / (1.0 - gamma));

					/* And now: construct the new state vector */
					localug [0, j].a = localpg [0, j] / (rgas * localtg [0, j]);
					localug [0, j].b = localug [0, j].a * u;
					localug [0, j].c = localug [0, j].a * v;
					localug [0, j].d = localug [0, j].a * (Cv * tff + 0.5 * (u * u + v * v));
				}
				/* Other Options */
				else {
					throw new Exception ("You have inflow at the outlet, which is not allowed.");
				}
			}
			/* Do something with corners to avoid division by zero errors */
			/* What you do shouldn't matter */
			localug [0, 0] = localug [1, 0];
			localug [imax, 0] = localug [imax, 1];
			localug [0, jmax] = localug [1, jmax];
			localug [imax, jmax] = localug [imax, jmax - 1];
		}

		public void runiters ()
		{

			for (int i = 0; i < iter; i++) {
				Euler.logger.InfoFormat ("Iteration: " + i.ToString () + "...");
				doIteration ();
			}
		}

	}

	public class Statevector
	{
		public double a;
		/* Storage for Statevectors */
		public double b;
		public double c;
		public double d;

		public Statevector ()
		{
			a = 0.0;
			b = 0.0;
			c = 0.0;
			d = 0.0;
		}

		/* Most of these vector manipulation routines are not used in this program */
		/* because I inlined them for speed.  I leave them here because they may */
		/* be useful in the future. */
		public Statevector amvect (double m, Statevector that)
		{
			/* Adds statevectors multiplies the sum by scalar m */
			Statevector answer = new Statevector ();

			answer.a = m * (this.a + that.a);
			answer.b = m * (this.b + that.b);
			answer.c = m * (this.c + that.c);
			answer.d = m * (this.d + that.d);

			return answer;
		}

		public Statevector avect (Statevector that)
		{
			Statevector answer = new Statevector ();
			/* Adds two statevectors */
			answer.a = this.a + that.a;
			answer.b = this.b + that.b;
			answer.c = this.c + that.c;
			answer.d = this.d + that.d;

			return answer;
		}

		public Statevector mvect (double m)
		{
			Statevector answer = new Statevector ();
			/* Multiplies statevector scalar m */
			answer.a = m * this.a;
			answer.b = m * this.b;
			answer.c = m * this.c;
			answer.d = m * this.d;

			return answer;
		}

		public Statevector svect (Statevector that)
		{
			Statevector answer = new Statevector ();
			/* Subtracts vector that from this */
			answer.a = this.a - that.a;
			answer.b = this.b - that.b;
			answer.c = this.c - that.c;
			answer.d = this.d - that.d;

			return answer;
		}

		public Statevector smvect (double m, Statevector that)
		{
			Statevector answer = new Statevector ();
			/* Subtracts statevector that from this and multiplies the */
			/* result by scalar m */
			answer.a = m * (this.a - that.a);
			answer.b = m * (this.b - that.b);
			answer.c = m * (this.c - that.c);
			answer.d = m * (this.d - that.d);

			return answer;
		}
	}

	public class Vector2
	{
		public double ihat;
		/* Storage for 2-D vector */
		public double jhat;

		public Vector2 ()
		{
			ihat = 0.0;
			jhat = 0.0;
		}

		public double magnitude ()
		{
			double mag;

			mag = Math.Sqrt (this.ihat * this.ihat + this.jhat * this.jhat);
			return mag;
		}

		public double dot (Vector2 that)
		{
			/* Calculates dot product of two 2-d vector */
			double answer;

			answer = this.ihat * that.ihat + this.jhat * that.jhat;

			return answer;
		}
	}

	class Constants
	{
		public const string INPUT = @"33	9
-1.00000000000000000000	0.00000000000000000000	1.01633253274924300000	0.69594989322316947000	0.00012423415727022292	2.06440385282092590000
-1.00000000000000000000	0.05183200000000000300	1.01623485123881880000	0.69604847097630540000	0.00048005265056625637	2.06427196842501550000
-1.00000000000000000000	0.11069800000000000000	1.01582974068940460000	0.69619432160505179000	0.00091147084760581437	2.06347825676689790000
-1.00000000000000000000	0.17881400000000000000	1.01491997162403070000	0.69636927544076910000	0.00122984966200296620	2.06156411952970900000
-1.00000000000000000000	0.25963700000000001000	1.01372792777759630000	0.69678264615202623000	0.00185618756502228330	2.05919893807544740000
-1.00000000000000000000	0.35902200000000001000	1.01144980098635930000	0.69718950150466819000	0.00226509766443798100	2.05440670569913310000
-1.00000000000000000000	0.48811700000000002000	1.00672135520523540000	0.69755064504050002000	0.00124338328625856610	2.04407851533280560000
-1.00000000000000000000	0.67264100000000004000	1.00214387014327920000	0.69829226247499421000	-0.00000102359133096300	2.03438133257328380000
-1.00000000000000000000	1.00000000000000000000	1.00174746698327420000	0.69846869929253541000	0.00004317668610895152	2.03361412607980930000
-0.70926999999999996000	0.00000000000000000000	1.05796345195146110000	0.67725973876036194000	0.00111046177346119750	2.15179413372528260000
-0.70926999999999996000	0.05183200000000000300	1.05751012853604380000	0.67751255648185649000	0.00352422860316615370	2.15085529179139460000
-0.70926999999999996000	0.11069800000000000000	1.05653342262020080000	0.67806951486219902000	0.00633144787309188920	2.14885385803719230000
-0.70926999999999996000	0.17881400000000000000	1.05460177495404680000	0.67889663564238178000	0.00917498247914409240	2.14472982195419390000
-0.70926999999999996000	0.25963700000000001000	1.05151375825685280000	0.68032924964044883000	0.01218465739153269700	2.13820234778612050000
-0.70926999999999996000	0.35902200000000001000	1.04815203675830860000	0.68346070604264331000	0.01621044744122422300	2.13203953290066610000
-0.70926999999999996000	0.48811700000000002000	1.03928424329118710000	0.68869340282605673000	0.01519569852782632700	2.11416201040839400000
-0.70926999999999996000	0.67264100000000004000	1.02510970125826220000	0.69315367854403853000	0.00483253095402537070	2.08352628034512490000
-0.70926999999999996000	1.00000000000000000000	1.02267599407972230000	0.69395980206853358000	-0.00467040130806209430	2.07839923908534980000
-0.50896200000000003000	0.00000000000000000000	1.09224263458651420000	0.65697903931215418000	0.00249258845019721110	2.22067164018384760000
-0.50896200000000003000	0.05183200000000000300	1.09145264397989570000	0.65746285713917130000	0.00763651469101566010	2.21900852546898530000
-0.50896200000000003000	0.11069800000000000000	1.08993816434874220000	0.65860359136561897000	0.01325513708210598500	2.21597254409167730000
-0.50896200000000003000	0.17881400000000000000	1.08790242141573270000	0.66037800496900889000	0.01913070058243483300	2.21205555009273920000
-0.50896200000000003000	0.25963700000000001000	1.08418148726586570000	0.66289092022265084000	0.02378396865119880600	2.20445879209686210000
-0.50896200000000003000	0.35902200000000001000	1.08310199517923930000	0.66849688587878886000	0.03012032539165209600	2.20510211285278990000
-0.50896200000000003000	0.48811700000000002000	1.07994746678201010000	0.67753190993651657000	0.03040154609088103300	2.20285450483177940000
-0.50896200000000003000	0.67264100000000004000	1.06628429847673980000	0.68360585994055478000	0.01136336656438763500	2.17360960365281120000
-0.50896200000000003000	1.00000000000000000000	1.06215336639509550000	0.68504835900528005000	-0.01131596936490563200	2.16472718078217060000
-0.35597699999999999000	0.00000000000000000000	1.09961508123696540000	0.64163470959846702000	0.00413284145327335110	2.22840276753115420000
-0.35597699999999999000	0.05183200000000000300	1.09854844306001740000	0.64311141571139496000	0.01203672248031600900	2.22695964870631300000
-0.35597699999999999000	0.11069800000000000000	1.09634815054222460000	0.64584465837108362000	0.01962413051003893100	2.22327863667056520000
-0.35597699999999999000	0.17881400000000000000	1.09472054989934040000	0.65010974515993392000	0.02715458885717425200	2.22178034596011290000
-0.35597699999999999000	0.25963700000000001000	1.09197244876150950000	0.65483743633499769000	0.03108048884882666200	2.21773287424267270000
-0.35597699999999999000	0.35902200000000001000	1.09433416013543330000	0.66230253175522491000	0.03459245201950703800	2.22768898973078810000
-0.35597699999999999000	0.48811700000000002000	1.10447666054088600000	0.67145197436947490000	0.03542066017566195000	2.25753711813443880000
-0.35597699999999999000	0.67264100000000004000	1.10550960961960800000	0.67446108402010074000	0.01545248136262992700	2.26151315572051860000
-0.35597699999999999000	1.00000000000000000000	1.10191689509553110000	0.67584655793540882000	-0.01580860495147432300	2.25370585720417750000
-0.23217499999999999000	0.00000000000000000000	1.10780476938693970000	0.63326503745992113000	0.01001625140154585900	2.24246484617440830000
-0.23217499999999999000	0.05183200000000000300	1.10149411265350630000	0.63579576365184853000	0.02467174488416347100	2.22918462894400000000
-0.23217499999999999000	0.11069800000000000000	1.09601089105427230000	0.64072620603402897000	0.03222249907475073500	2.21925040056340660000
-0.23217499999999999000	0.17881400000000000000	1.09144390502951370000	0.64811110090497182000	0.03813159404206482900	2.21261676122838890000
-0.23217499999999999000	0.25963700000000001000	1.08794910661530490000	0.65599066300055775000	0.03814394724805182700	2.20860476534821440000
-0.23217499999999999000	0.35902200000000001000	1.08947082387554550000	0.66536791336352696000	0.03389354166197270500	2.21755339364647290000
-0.23217499999999999000	0.48811700000000002000	1.10637106737147150000	0.67387399119556846000	0.03151546722824204900	2.26330832912699000000
-0.23217499999999999000	0.67264100000000004000	1.12141086706878750000	0.67346477903889235000	0.01597405424404546900	2.29923653214680090000
-0.23217499999999999000	1.00000000000000000000	1.12005230534746620000	0.67411014296907001000	-0.01667682615648040700	2.29634311499993340000
-0.12818499999999999000	0.00000000000000000000	1.11877183076426730000	0.57490282011781846000	0.01866798003599267100	2.23900836588505750000
-0.12818499999999999000	0.05183200000000000300	1.10659757644166310000	0.61023062510783010000	0.04376703018594321800	2.22673165827400950000
-0.12818499999999999000	0.11069800000000000000	1.09777803096620710000	0.62908819972578300000	0.05118528905599666800	2.21635383252731180000
-0.12818499999999999000	0.17881400000000000000	1.08915017665832710000	0.64494887943985990000	0.05314930161925994500	2.20515768919902920000
-0.12818499999999999000	0.25963700000000001000	1.08420844907632550000	0.65852485607551314000	0.04780753532059321000	2.20121389369962280000
-0.12818499999999999000	0.35902200000000001000	1.08251612252566500000	0.67088996921907118000	0.03517491630707601800	2.20420827345563630000
-0.12818499999999999000	0.48811700000000002000	1.09656136792761760000	0.68055735585494626000	0.02690444830506533500	2.24367282232237650000
-0.12818499999999999000	0.67264100000000004000	1.11527583653773690000	0.68037148454825025000	0.01497627572449967100	2.28849995695042760000
-0.12818499999999999000	1.00000000000000000000	1.11579951804086020000	0.68038193092447241000	-0.01576811665569551400	2.28979392889044050000
-0.03853299999999999800	0.00000000000000000000	1.15797252050243540000	0.53866998389067799000	0.08827217724448255300	2.32142876671935160000
-0.03853299999999999800	0.05183200000000000300	1.11332310893114330000	0.59284314386577808000	0.10936552494826596000	2.23844841062516100000
-0.03853299999999999800	0.11069800000000000000	1.10202612879058210000	0.62929370317256772000	0.09173108707983901300	2.22922895045024250000
-0.03853299999999999800	0.17881400000000000000	1.08627520282973870000	0.65094211489397635000	0.07882949415340172200	2.20330236714907370000
-0.03853299999999999800	0.25963700000000001000	1.07951326560824020000	0.66723175603440632000	0.06132507509941036200	2.19599844841337080000
-0.03853299999999999800	0.35902200000000001000	1.07558692618880310000	0.67987735315588671000	0.03950180032807805400	2.19353204676354440000
-0.03853299999999999800	0.48811700000000002000	1.08493981475182790000	0.68889664437554543000	0.02483876981237407100	2.22107042422938820000
-0.03853299999999999800	0.67264100000000004000	1.10222319720784930000	0.68934246801538435000	0.01398972620397986000	2.26253703015229890000
-0.03853299999999999800	1.00000000000000000000	1.10356521996582970000	0.68913673268003506000	-0.01473987180995553800	2.26565840916990610000
0.04026099999999999800	0.01599300000000000000	1.10869590212957880000	0.60337215587318771000	0.16280148278131434000	2.24893971312382760000
0.04026099999999999800	0.06699600000000000000	1.07210876188135070000	0.62951962526788696000	0.15612865242552734000	2.16911626943324800000
0.04026099999999999800	0.12492100000000000000	1.07363133442858930000	0.66434768396732446000	0.11766650232466615000	2.18587874698278690000
0.04026099999999999800	0.19194800000000001000	1.06251702079552080000	0.67622630523809835000	0.09375511921402818200	2.16422205406828820000
0.04026099999999999800	0.27147800000000000000	1.06225247573480660000	0.68647788033107005000	0.06671389754819155600	2.16785094563094690000
0.04026099999999999800	0.36927300000000002000	1.06192341263424270000	0.69358686153695992000	0.03995345199588715800	2.17021794557562850000
0.04026099999999999800	0.49630299999999999000	1.07191395728369580000	0.69820195353438619000	0.02231092314202768800	2.19615284639389240000
0.04026099999999999800	0.67787699999999995000	1.08993579501272350000	0.69738595617957544000	0.01285178210534825400	2.23820851038716380000
0.04026099999999999800	1.00000000000000000000	1.09167059990119660000	0.69712985603590705000	-0.01355847476995183200	2.24220772240397800000
0.11054400000000000000	0.04029200000000000100	1.01564308667504570000	0.67965353284580410000	0.16636392938051639000	2.07220764218164800000
0.11054400000000000000	0.09003500000000000400	1.01386234212294620000	0.67789516200530398000	0.14145842078303336000	2.06058165385093120000
0.11054400000000000000	0.14652999999999999000	1.02846787889707000000	0.69988496450976745000	0.10848242475554633000	2.10202833072914960000
0.11054400000000000000	0.21190100000000001000	1.03005301032367380000	0.70164910319035356000	0.08703975822267048800	2.10463428200317000000
0.11054400000000000000	0.28946800000000000000	1.03936051814643120000	0.70565856748801592000	0.06083220076402304200	2.12678627033441850000
0.11054400000000000000	0.38484800000000002000	1.04446747439283820000	0.70711313283068289000	0.03518987246076288300	2.13834025486602240000
0.11054400000000000000	0.50874100000000000000	1.05730869150562050000	0.70710932482476019000	0.01803678847092847100	2.16772693673055360000
0.11054400000000000000	0.68583099999999997000	1.07761066442101950000	0.70465003509083857000	0.01130125030278132700	2.21367152704824170000
0.11054400000000000000	1.00000000000000000000	1.07972969685425910000	0.70434541691899355000	-0.01196748825869488300	2.21850904330976960000
0.17397799999999999000	0.05845500000000000000	0.97891959810793205000	0.71546248953906044000	0.15496833698063617000	2.01126564758218820000
0.17397799999999999000	0.10725700000000001000	0.99169390744141217000	0.70765494646149807000	0.12305762223205813000	2.02790377165308520000
0.17397799999999999000	0.16268299999999999000	1.00404784894586880000	0.72106044903126965000	0.09743445591950637700	2.06001150886598210000
0.17397799999999999000	0.22681699999999999000	1.01006577469050600000	0.71937888465560440000	0.07724426099022518900	2.07052137752691000000
0.17397799999999999000	0.30291499999999999000	1.02195108968995840000	0.72046127567363505000	0.05304256851361175600	2.09661496735462860000
0.17397799999999999000	0.39649000000000001000	1.02890160847309460000	0.71833428171196911000	0.02912011613940910700	2.11003295911997090000
0.17397799999999999000	0.51803900000000003000	1.04272323631536130000	0.71496901275417857000	0.01314646217629766000	2.13917686824517880000
0.17397799999999999000	0.69177699999999998000	1.06446334618897830000	0.71141832457612864000	0.00951227204015934680	2.18725155641816690000
0.17397799999999999000	1.00000000000000000000	1.06690399424879940000	0.71108752981957390000	-0.01012417549197022900	2.19277488889220610000
0.23177900000000001000	0.07202899999999999600	0.94438611064577238000	0.73877092371312136000	0.13595491365394868000	1.95115131956350330000
0.23177900000000001000	0.12012700000000000000	0.96700013819978747000	0.73180506010036694000	0.10282482800000692000	1.98853428715253200000
0.23177900000000001000	0.17475399999999999000	0.97878811163166102000	0.73749670295078640000	0.08399520817672123700	2.01486067238799250000
0.23177900000000001000	0.23796300000000001000	0.98999273022109935000	0.73418124605556279000	0.06509691716517894400	2.03552127170242510000
0.23177900000000001000	0.31296499999999999000	1.00437824282230030000	0.73298653108235545000	0.04393663935117708700	2.06547402834463640000
0.23177900000000001000	0.40519100000000002000	1.01356534155597240000	0.72795577891211549000	0.02228489388116931100	2.08183333309974470000
0.23177900000000001000	0.52498699999999998000	1.02837897690066790000	0.72176738527681261000	0.00802543201631003220	2.11096854463419700000
0.23177900000000001000	0.69621999999999995000	1.05126454525022010000	0.71745281149256290000	0.00762151542590231920	2.16060932328436190000
0.23177900000000001000	1.00000000000000000000	1.05393254601846120000	0.71712214021902621000	-0.00817531455852247430	2.16660239816113840000
0.28486699999999998000	0.08207599999999999600	0.91052613009156780000	0.75509701500143889000	0.11362674228968718000	1.89061050627546520000
0.28486699999999998000	0.12965299999999999000	0.94139530311376263000	0.75114438264518346000	0.08104015895069316400	1.94607167591771880000
0.28486699999999998000	0.18368799999999999000	0.95368665860425961000	0.75050159888486523000	0.06821263851966571900	1.96920666620445870000
0.28486699999999998000	0.24621299999999999000	0.96973893613038753000	0.74649800738635330000	0.05106233923008296500	1.99951788609060350000
0.28486699999999998000	0.32040299999999999000	0.98649449417018253000	0.74350235858896774000	0.03358090241128704200	2.03310757647397720000
0.28486699999999998000	0.41163100000000002000	0.99813212564765941000	0.73619066768260988000	0.01469416565718445700	2.05307638994073380000
0.28486699999999998000	0.53012999999999999000	1.01423662859847230000	0.72759830606934339000	0.00267014599104141350	2.08302608458076530000
0.28486699999999998000	0.69950900000000005000	1.03842714756603600000	0.72268257372173272000	0.00565756203799700100	2.13462963913309610000
0.28486699999999998000	1.00000000000000000000	1.04127883291479820000	0.72236446717123226000	-0.00615582330375045570	2.14099230463437400000
0.33395300000000000000	0.08935200000000000100	0.88136672919543979000	0.76537871642571131000	0.09060742791957718300	1.83791259921236370000
0.33395300000000000000	0.13655200000000001000	0.91893617716589504000	0.76510758189485573000	0.05912837316733302000	1.90844001048407600000
0.33395300000000000000	0.19015899999999999000	0.93139159009780792000	0.75970885717261794000	0.05164580598331647300	1.92828285952289580000
0.33395300000000000000	0.25218900000000000000	0.95107713443782027000	0.75588067316789009000	0.03636295601053488700	1.96595890581146620000
0.33395300000000000000	0.32579000000000002000	0.96948668879049382000	0.75180887174491395000	0.02262535666411547400	2.00192449330614730000
0.33395300000000000000	0.41629500000000003000	0.98315753276487605000	0.74298222982686613000	0.00665719796123354080	2.02491164296397400000
0.33395300000000000000	0.53385400000000005000	1.00043303729351510000	0.73248473439653883000	-0.00284987374316114300	2.05562783127500830000
0.33395300000000000000	0.70189100000000004000	1.02593710054782790000	0.72717497275097309000	0.00363884249729646550	2.10928340821999380000
0.33395300000000000000	1.00000000000000000000	1.02894616975380910000	0.72687079670847099000	-0.00408466127112979030	2.11595102962063650000
0.37959700000000002000	0.09441199999999999600	0.85539145803508621000	0.77160158761724162000	0.06790230830057748800	1.79076853584451000000
0.37959700000000002000	0.14135000000000000000	0.89845091789570608000	0.77555932161320895000	0.03779776494739912200	1.87403405199496500000
0.37959700000000002000	0.19465900000000000000	0.91107355470475304000	0.76665295685510271000	0.03528625699194398000	1.89104716623659330000
0.37959700000000002000	0.25634400000000002000	0.93368524732735581000	0.76324741777833638000	0.02193568413452788200	1.93456849262718640000
0.37959700000000002000	0.32953700000000002000	0.95336852990006649000	0.75837642198105570000	0.01172036122374116300	1.97214279915973000000
0.37959700000000002000	0.41953800000000002000	0.96876612002105367000	0.74847229657712711000	-0.00143824699933451970	1.99763160513722270000
0.37959700000000002000	0.53644499999999995000	0.98707847085334210000	0.73647849352349759000	-0.00837059182039250930	2.02900055561440330000
0.37959700000000002000	0.70354799999999995000	1.01383155714645200000	0.73098721494331775000	0.00161177178170554940	2.08465084730945760000
0.37959700000000002000	1.00000000000000000000	1.01697695243399710000	0.73070351249627830000	-0.00200847656826816470	2.09157657286486250000
0.42225299999999999000	0.09767299999999999600	0.83135878610049696000	0.77466199207757058000	0.04596167161529404700	1.74629195918086770000
0.42225299999999999000	0.14444199999999999000	0.87888704410562069000	0.78244375526981513000	0.01717325713245707000	1.84006273645804200000
0.42225299999999999000	0.19755900000000001000	0.89176180705710950000	0.77096976368785819000	0.01896282398178425500	1.85497641910294210000
0.42225299999999999000	0.25902199999999997000	0.91692207772205958000	0.76846016584945198000	0.00748574640486719850	1.90375419615568300000
0.42225299999999999000	0.33195100000000000000	0.93783630647299576000	0.76335046028004794000	0.00067551712973560945	1.94310427171026020000
0.42225299999999999000	0.42162800000000000000	0.95488316159279041000	0.75287515415637907000	-0.00964615574754882670	1.97115231016371070000
0.42225299999999999000	0.53811399999999998000	0.97419223604359206000	0.73971801835783457000	-0.01391221798709984900	2.00323341589133360000
0.42225299999999999000	0.70461499999999999000	1.00215405146211660000	0.73418463980596194000	-0.00042721787450638975	2.06083725051510890000
0.42225299999999999000	1.00000000000000000000	1.00541663205978730000	0.73391966224136052000	0.00007745983642219967	2.06797441112966540000
0.46228500000000000000	0.09945300000000000000	0.80825074300769073000	0.77512009069181820000	0.02506209175372302300	1.70367399757211290000
0.46228500000000000000	0.14613000000000001000	0.86041774723638809000	0.78743241358744553000	-0.00253276294775680930	1.80828799112923420000
0.46228500000000000000	0.19914200000000001000	0.87386368408682447000	0.77431445482937034000	0.00347382917884794810	1.82193675973315730000
0.46228500000000000000	0.26048300000000002000	0.90117250952322170000	0.77250999014433508000	-0.00609703195441201140	1.87483763869877570000
0.46228500000000000000	0.33326800000000001000	0.92305165865194772000	0.76712960029101529000	-0.00987312641410125790	1.91530668898030830000
0.46228500000000000000	0.42276900000000001000	0.94147732861131073000	0.75624912096328689000	-0.01760687597195548900	1.94536825995615100000
0.46228500000000000000	0.53902499999999998000	0.96172483833970979000	0.74220850360571400000	-0.01930726777886795200	1.97816588846153210000
0.46228500000000000000	0.70519799999999999000	0.99089962958546807000	0.73680635904747793000	-0.00242392199602476560	2.03781463691854150000
0.46228500000000000000	1.00000000000000000000	0.99426840912241654000	0.73656433181708991000	0.00211598279555167070	2.04514020880829110000
0.50000000000000000000	0.10000000000000001000	0.78707892681425728000	0.77392278192279507000	0.00491873645248108900	1.66340436375967720000
0.50000000000000000000	0.14664800000000000000	0.84281333062384212000	0.78960831661036845000	-0.02177964737717095300	1.77651214006970040000
0.50000000000000000000	0.19962800000000000000	0.85633284993660908000	0.77524135033941388000	-0.01199799046984418400	1.78852802419470370000
0.50000000000000000000	0.26093300000000003000	0.88553430296040581000	0.77475577803863060000	-0.01968896799359355300	1.84547810045840690000
0.50000000000000000000	0.33367400000000003000	0.90845739346234233000	0.76975564057044699000	-0.02043209485368169600	1.88759097699153890000
0.50000000000000000000	0.42312000000000000000	0.92832121638931142000	0.75889956424217653000	-0.02554668817560214200	1.91998355419574680000
0.50000000000000000000	0.53930500000000003000	0.94957229998886394000	0.74419730241320337000	-0.02466536801376462100	1.95371785864912170000
0.50000000000000000000	0.70537700000000003000	0.97997401816072671000	0.73897954636549890000	-0.00441312564409457840	2.01543597053598010000
0.50000000000000000000	1.00000000000000000000	0.98343537747378662000	0.73875733818516021000	0.00414586107150597890	2.02291606674685910000
0.53771500000000005000	0.09945300000000000000	0.76635025241330901000	0.76994038219561278000	-0.01617769413273692200	1.62461667551763940000
0.53771500000000005000	0.14613000000000001000	0.82527723984209389000	0.79108347127239709000	-0.04182182185976748800	1.74617288430012850000
0.53771500000000005000	0.19914200000000001000	0.83878566694711887000	0.77620651686502629000	-0.02773429126328742400	1.75602816118146010000
0.53771500000000005000	0.26048300000000002000	0.86945150972339968000	0.77664491830640126000	-0.03358014058237866800	1.81556925925952140000
0.53771500000000005000	0.33326800000000001000	0.89312650026751750000	0.77175738765729440000	-0.03156078444290676100	1.85841432823961130000
0.53771500000000005000	0.42276900000000001000	0.91433385244140153000	0.76090663864152275000	-0.03414041489886791800	1.89280972725818250000
0.53771500000000005000	0.53902499999999998000	0.93669171089624059000	0.74565396614442403000	-0.03053416494195206700	1.92765507901878430000
0.53771500000000005000	0.70519799999999999000	0.96848586221042432000	0.74080468853379677000	-0.00660360129014163090	1.99182429675642750000
0.53771500000000005000	1.00000000000000000000	0.97203544943197451000	0.74060978656962506000	0.00637648434136586000	1.99944845146682540000
0.57774700000000001000	0.09767299999999999600	0.74179533217798954000	0.76499787705913325000	-0.03788723663869168800	1.57651721428961620000
0.57774700000000001000	0.14444199999999999000	0.80331201249275241000	0.78922872388176146000	-0.06242147319483677000	1.70468512462224830000
0.57774700000000001000	0.19755900000000001000	0.81725658463768935000	0.77407482889638657000	-0.04502314853320501400	1.71419953242085790000
0.57774700000000001000	0.25902199999999997000	0.84994170873849484000	0.77665044313085674000	-0.04896153680781807600	1.77837243831607080000
0.57774700000000001000	0.33195100000000000000	0.87535903043636121000	0.77275368024336244000	-0.04386177477215755900	1.82431776317083980000
0.57774700000000001000	0.42162800000000000000	0.89868721353532621000	0.76236041364940488000	-0.04362976194660971000	1.86234077505121070000
0.57774700000000001000	0.53811399999999998000	0.92260539995880253000	0.74671207364579684000	-0.03700383290055651900	1.89912947702542210000
0.57774700000000001000	0.70461499999999999000	0.95599559826838931000	0.74232083302505758000	-0.00902609636307812570	1.96609191961181900000
0.57774700000000001000	1.00000000000000000000	0.95962283271108995000	0.74215368285381245000	0.00884149314697615410	1.97382848600048180000
0.62040300000000004000	0.09441199999999999600	0.69358027691195823000	0.75128018076745728000	-0.05832202602801412500	1.48821174848046180000
0.62040300000000004000	0.14135000000000000000	0.76872490517065073000	0.78590076672871889000	-0.08279766784841050000	1.64351206805522290000
0.62040300000000004000	0.19465900000000000000	0.78873487162426426000	0.77228520187662131000	-0.06172077955423192800	1.66183160826405850000
0.62040300000000004000	0.25634400000000002000	0.82660211819090923000	0.77615786434722023000	-0.06441015643699693500	1.73459205836441080000
0.62040300000000004000	0.32953700000000002000	0.85554777272589799000	0.77286492992866329000	-0.05720481397567823300	1.78630106408582010000
0.62040300000000004000	0.41953800000000002000	0.88176958835680597000	0.76288499290295786000	-0.05423720583814265600	1.82917987846967070000
0.62040300000000004000	0.53644499999999995000	0.90762487693586935000	0.74697142647466086000	-0.04425909128071287000	1.86857423869059970000
0.62040300000000004000	0.70354799999999995000	0.94275736982767566000	0.74330823799153711000	-0.01173371720918629600	1.93867551685359720000
0.62040300000000004000	1.00000000000000000000	0.94644346142843438000	0.74317966519170475000	0.01159174339541884000	1.94648510040035090000
0.66604699999999994000	0.08935200000000000100	0.66767019917843040000	0.73848352046369414000	-0.08904945470971660500	1.43278309583094290000
0.66604699999999994000	0.13655200000000001000	0.73849407610406415000	0.77746639110979199000	-0.11663182225282001000	1.58592642868623780000
0.66604699999999994000	0.19015899999999999000	0.75957456656974121000	0.76550237260531728000	-0.08755919853355774400	1.60565268541392010000
0.66604699999999994000	0.25218900000000000000	0.80488382100841449000	0.77314899286228611000	-0.08595866626472165000	1.69337190076362100000
0.66604699999999994000	0.32579000000000002000	0.83767551191070067000	0.77135138494307576000	-0.07380673174078235300	1.75190361134771780000
0.66604699999999994000	0.41629500000000003000	0.86629799551574438000	0.76212446208250761000	-0.06668409359976518200	1.79862127508030810000
0.66604699999999994000	0.53385400000000005000	0.89309197099778503000	0.74625287026864562000	-0.05248373881880817600	1.83864527523151430000
0.66604699999999994000	0.70189100000000004000	0.92915994446187145000	0.74361247078787374000	-0.01475025543524193800	1.91032065049523460000
0.66604699999999994000	1.00000000000000000000	0.93284654861518179000	0.74352893952276811000	0.01465505728076897100	1.91808168811256260000
0.71513300000000002000	0.08207599999999999600	0.77285472483941786000	0.74338176639300180000	-0.12721013795270447000	1.62577868033486110000
0.71513300000000002000	0.12965299999999999000	0.79956848566433247000	0.77558362427779204000	-0.14773228921109255000	1.69654476171072920000
0.71513300000000002000	0.18368799999999999000	0.79689966946671054000	0.75937519888658178000	-0.11949182711663890000	1.66982614762403570000
0.71513300000000002000	0.24621299999999999000	0.82388022530476845000	0.76706895875217385000	-0.11468459489645678000	1.72562760143620200000
0.71513300000000002000	0.32040299999999999000	0.84231318036149949000	0.76664615054007257000	-0.09453950432168567500	1.75800823102122990000
0.71513300000000002000	0.41163100000000002000	0.86214313914626739000	0.75875435814576031000	-0.08121743233806218200	1.78853035751326920000
0.71513300000000002000	0.53012999999999999000	0.88262637345384498000	0.74366387856250948000	-0.06150852123597104500	1.81586987385555650000
0.71513300000000002000	0.69950900000000005000	0.91616601678500309000	0.74284174545191262000	-0.01800614204194279600	1.88273439028157590000
0.71513300000000002000	1.00000000000000000000	0.91973036518744600000	0.74282140022512810000	0.01797217388166414900	1.89021224315262850000
0.76822100000000004000	0.07202899999999999600	0.93189887202931965000	0.71186576315208150000	-0.15183057140532644000	1.92594579459352830000
0.76822100000000004000	0.12012700000000000000	0.93174300488971307000	0.75164832896675726000	-0.15597919715152475000	1.94341225352019740000
0.76822100000000004000	0.17475399999999999000	0.91105656891022835000	0.74140916153609826000	-0.13491632613986654000	1.88324864903868860000
0.76822100000000004000	0.23796300000000001000	0.90369795583746171000	0.75053496866765579000	-0.12950164056155833000	1.87238026935090710000
0.76822100000000004000	0.31296499999999999000	0.89108953215447684000	0.75456532009573696000	-0.10798498312895695000	1.84586792642889640000
0.76822100000000004000	0.40519100000000002000	0.88419457506461108000	0.75076635116671031000	-0.09260477087091098500	1.82661495645329500000
0.76822100000000004000	0.52498699999999998000	0.88326531239216155000	0.73844291802419504000	-0.06932790952309088300	1.81332012073198330000
0.76822100000000004000	0.69621999999999995000	0.90604598558880245000	0.74065155282980921000	-0.02110086488378592700	1.86023500651739200000
0.76822100000000004000	1.00000000000000000000	0.90924881043888783000	0.74070893546867456000	0.02116784291396133600	1.86697625195260960000
0.82602200000000003000	0.05845500000000000000	1.01944315737051490000	0.67817244688219480000	-0.16358090433324252000	2.09654731771127120000
0.82602200000000003000	0.10725700000000001000	1.02083143399155850000	0.72273712311368354000	-0.14926395336494236000	2.11579537759503240000
0.82602200000000003000	0.16268299999999999000	1.00474984452226070000	0.71817859699267461000	-0.12734741310432474000	2.06669050522930410000
0.82602200000000003000	0.22681699999999999000	0.98615112890951229000	0.72855068179627891000	-0.11879569761326185000	2.02990228210849640000
0.82602200000000003000	0.30291499999999999000	0.95943559414211010000	0.73730247551087136000	-0.10104858536249289000	1.97482910007104050000
0.82602200000000003000	0.39649000000000001000	0.92879945483906012000	0.73836762939657052000	-0.08984679560205159600	1.90854544531143460000
0.82602200000000003000	0.51803900000000003000	0.89910222818031804000	0.73034334233078280000	-0.07059599077743562700	1.83906536809686650000
0.82602200000000003000	0.69177699999999998000	0.90207809114790960000	0.73659525850009933000	-0.02295305209550337800	1.84894464596610810000
0.82602200000000003000	1.00000000000000000000	0.90458682594706674000	0.73672112558957648000	0.02317975630956098400	1.85430754850885890000
0.88945600000000002000	0.04029200000000000100	1.04916365208010800000	0.59771873974047096000	-0.17056657164184374000	2.12331682962643510000
0.88945600000000002000	0.09003500000000000400	1.04570208881854200000	0.68325394946983098000	-0.14551612636958702000	2.14793316048968740000
0.88945600000000002000	0.14652999999999999000	1.03680704198137460000	0.69754869362182603000	-0.12300070762785373000	2.12499734623158650000
0.88945600000000002000	0.21190100000000001000	1.02041853192845710000	0.71084884291501471000	-0.10698325122228668000	2.09229318309576890000
0.88945600000000002000	0.28946800000000000000	1.00053294201605540000	0.72254348559116799000	-0.08745770796115318000	2.05262983932519290000
0.88945600000000002000	0.38484800000000002000	0.96867600844134316000	0.72650497208959974000	-0.07551467054879966400	1.98393646006985520000
0.88945600000000002000	0.50874100000000000000	0.92335730185658804000	0.72138248660561366000	-0.06314620202946051600	1.88228011969870070000
0.88945600000000002000	0.68583099999999997000	0.90682244940879631000	0.73102072191462297000	-0.02302540404790504700	1.85436697019362030000
0.88945600000000002000	1.00000000000000000000	0.90841833884175593000	0.73121215492356895000	0.02339769294360303700	1.85791824650392460000
0.95973900000000001000	0.01599300000000000000	1.10418186404474120000	0.52771429619482046000	-0.11933650086955920000	2.21274320204869610000
0.95973900000000001000	0.06699600000000000000	1.07030634477072640000	0.63357579954255372000	-0.09540529203036506000	2.16939208251455180000
0.95973900000000001000	0.12492100000000000000	1.05893193762065520000	0.67441277545495693000	-0.09841361617604745000	2.15783281209081190000
0.95973900000000001000	0.19194800000000001000	1.03363490089632170000	0.69379687868108753000	-0.08804453740738557600	2.10802306234016030000
0.95973900000000001000	0.27147800000000000000	1.01541916537988810000	0.70939744673557259000	-0.07301960805650900600	2.07504792248204680000
0.95973900000000001000	0.36927300000000002000	0.99013988177424905000	0.71632402072008383000	-0.05844898862759559400	2.02249401273472260000
0.95973900000000001000	0.49630299999999999000	0.94555857833444101000	0.71312058669006262000	-0.05002364892710584000	1.92296266186148120000
0.95973900000000001000	0.67787699999999995000	0.91901509112597557000	0.72403671028389816000	-0.02136955408994570300	1.87439546534188730000
0.95973900000000001000	1.00000000000000000000	0.91976959974001027000	0.72424938945263473000	0.02182118613105065600	1.87621736647993890000
1.03853299999999990000	0.00000000000000000000	1.10714334501950140000	0.57093118133678378000	-0.03355598127522086600	2.24029526034277810000
1.03853299999999990000	0.05183200000000000300	1.08291092638053590000	0.63513345914651120000	-0.02832609217694476800	2.19813703866234980000
1.03853299999999990000	0.11069800000000000000	1.07276262123663990000	0.67877084964963841000	-0.04787562516540869900	2.18991709489482080000
1.03853299999999990000	0.17881400000000000000	1.04330424810195170000	0.69124108330271938000	-0.05065895345454046000	2.12566743894389010000
1.03853299999999990000	0.25963700000000001000	1.02255188056214740000	0.70286476034882484000	-0.04878714913081343100	2.08508848628417320000
1.03853299999999990000	0.35902200000000001000	0.99994259358611703000	0.70846558364351953000	-0.03858617840639123900	2.03794480999008520000
1.03853299999999990000	0.48811700000000002000	0.96218411525391179000	0.70636819197801604000	-0.03491893145781277400	1.95382054523671120000
1.03853299999999990000	0.67264100000000004000	0.93451941661241666000	0.71693090386023028000	-0.01868525286275223000	1.90184626986214480000
1.03853299999999990000	1.00000000000000000000	0.93476815146151027000	0.71720923427607663000	0.01912235021715539400	1.90264424686219290000
1.12818500000000000000	0.00000000000000000000	1.03972083187174900000	0.62924771127212764000	-0.01131117499454207400	2.10267323784420370000
1.12818500000000000000	0.05183200000000000300	1.04648462883074410000	0.66083760181622020000	-0.01463403607037418300	2.12530248443562140000
1.12818500000000000000	0.11069800000000000000	1.04705454038424330000	0.69206167658414186000	-0.02271927380866790200	2.13737497977290980000
1.12818500000000000000	0.17881400000000000000	1.03130432841601550000	0.69629845764510157000	-0.02567348947658805100	2.10074611501170240000
1.12818500000000000000	0.25963700000000001000	1.01802796753187060000	0.70076523195079432000	-0.02757702141104180500	2.07314001348308620000
1.12818500000000000000	0.35902200000000001000	1.00224258077390130000	0.70335583033633853000	-0.02066158164610946600	2.03955886666145060000
1.12818500000000000000	0.48811700000000002000	0.97395924656326216000	0.70192114190565269000	-0.02084917007866912000	1.97655012749492840000
1.12818500000000000000	0.67264100000000004000	0.94900406410686955000	0.71014679953275328000	-0.01543381486003460500	1.92805421056851010000
1.12818500000000000000	1.00000000000000000000	0.94893990667221650000	0.71038715067961022000	0.01581845342142165400	1.92813857471818780000
1.23217500000000000000	0.00000000000000000000	1.01788853906487240000	0.65174487494884814000	-0.00451914560744932310	2.05957092916111910000
1.23217500000000000000	0.05183200000000000300	1.02743873196007000000	0.67543346168718088000	-0.00743824699645905670	2.08836680477266250000
1.23217500000000000000	0.11069800000000000000	1.02777292990166420000	0.69885313205639588000	-0.00835230686481021330	2.09734803960369920000
1.23217500000000000000	0.17881400000000000000	1.01874689433437470000	0.70081022505324864000	-0.01116934694410449100	2.07546887393638850000
1.23217500000000000000	0.25963700000000001000	1.01131981038004940000	0.70047419676817724000	-0.01263988415152978200	2.05822017047336200000
1.23217500000000000000	0.35902200000000001000	1.00288506380354470000	0.70069205952213820000	-0.00701032626980942990	2.03974037091640390000
1.23217500000000000000	0.48811700000000002000	0.98343718347636988000	0.69974980196121339000	-0.00932862038451891250	1.99634524494585520000
1.23217500000000000000	0.67264100000000004000	0.96159553359891425000	0.70472334515052737000	-0.01194666994553815100	1.95167955035028220000
1.23217500000000000000	1.00000000000000000000	0.96139122270490107000	0.70499750780496739000	0.01235583333341742700	1.95142999572421270000
1.35597700000000000000	0.00000000000000000000	1.00467797382075030000	0.65843885538619473000	-0.00355929570002330890	2.03013860085960210000
1.35597700000000000000	0.05183200000000000300	1.01697222331091260000	0.68337405831796305000	-0.00356132101049765230	2.06820943815186540000
1.35597700000000000000	0.11069800000000000000	1.01797694760130760000	0.70312046737354583000	0.00049958820248128697	2.07763980767236990000
1.35597700000000000000	0.17881400000000000000	1.01147541426913980000	0.70453041387759507000	-0.00210883698719981780	2.06139811708546490000
1.35597700000000000000	0.25963700000000001000	1.00665598980281560000	0.70088546306633837000	-0.00285831037837292260	2.04804103830543970000
1.35597700000000000000	0.35902200000000001000	1.00299548389505990000	0.69901067601339528000	0.00149098960231753380	2.03916848054479600000
1.35597700000000000000	0.48811700000000002000	0.99074937863080892000	0.69838432177736609000	-0.00189759826753813890	2.01174444292146770000
1.35597700000000000000	0.67264100000000004000	0.97251884259346100000	0.70018195255558691000	-0.00913724279219609370	1.97220277641140230000
1.35597700000000000000	1.00000000000000000000	0.97222776251920284000	0.70042661269302731000	0.00959587980714557940	1.97165315972202330000
1.50896199999999990000	0.00000000000000000000	0.99626877395035152000	0.66867442112280728000	-0.00415784062506694210	2.01258873861233620000
1.50896199999999990000	0.05183200000000000300	1.00843514713890330000	0.69094745141185099000	-0.00336772438588442020	2.05092298618908990000
1.50896199999999990000	0.11069800000000000000	1.01013527889631600000	0.70509451995538408000	0.00217447484555702110	2.06017510221163170000
1.50896199999999990000	0.17881400000000000000	1.00575451786517740000	0.70553929677823635000	0.00082167584862451702	2.04882824004814970000
1.50896199999999990000	0.25963700000000001000	1.00272231827882560000	0.70103411274273053000	0.00077917196329018758	2.03909480380526190000
1.50896199999999990000	0.35902200000000001000	1.00181640712512370000	0.69864679510005312000	0.00365659572816478140	2.03602879883067620000
1.50896199999999990000	0.48811700000000002000	0.99584576042298545000	0.69828516543303132000	0.00103697673100834520	2.02245351756071660000
1.50896199999999990000	0.67264100000000004000	0.98289298314731866000	0.69700832701128246000	-0.00632224517170764270	1.99210048859912650000
1.50896199999999990000	1.00000000000000000000	0.98270649407291588000	0.69735703413000716000	0.00672716998587883850	1.99182913402493140000
1.70927000000000010000	0.00000000000000000000	0.99443066293963001000	0.68414510919162486000	-0.00245894062888764010	2.01087633701439250000
1.70927000000000010000	0.05183200000000000300	0.99931571878044767000	0.69299540149766470000	-0.00227313294438145910	2.02696036014201120000
1.70927000000000010000	0.11069800000000000000	1.00000024433295280000	0.69813212181050055000	0.00035783968567941395	2.03111140699994050000
1.70927000000000010000	0.17881400000000000000	0.99826579677890248000	0.69834203804117911000	-0.00006936619302191276	2.02707323279872930000
1.70927000000000010000	0.25963700000000001000	0.99728130716948904000	0.69673818172514979000	-0.00008360900503007445	2.02390986833914480000
1.70927000000000010000	0.35902200000000001000	0.99816445223017081000	0.69671956286570880000	0.00147827146483655810	2.02594300449474930000
1.70927000000000010000	0.48811700000000002000	0.99758760797665491000	0.69743975333292374000	0.00127340990507565250	2.02481440209573730000
1.70927000000000010000	0.67264100000000004000	0.99183042528688570000	0.69520649517964950000	-0.00271296570539258670	2.00983528438261330000
1.70927000000000010000	1.00000000000000000000	0.99173402203982175000	0.69536616954623354000	0.00286358376714936860	2.00969068730405680000
2.00000000000000000000	0.00000000000000000000	0.98893204982299698000	0.68601847747313072000	-0.00077441297957429357	1.99944460301373540000
2.00000000000000000000	0.05183200000000000300	0.99496482392452390000	0.69490417924340242000	-0.00089932956421385026	2.01826898378883610000
2.00000000000000000000	0.11069800000000000000	0.99758002774680332000	0.69817363724570369000	-0.00017900566354726887	2.02561281336582870000
2.00000000000000000000	0.17881400000000000000	0.99742247529225248000	0.69762004671702438000	-0.00069678371815480285	2.02466631825049200000
2.00000000000000000000	0.25963700000000001000	0.99665514787327270000	0.69657817756032725000	-0.00012683044870211407	2.02239449307789520000
2.00000000000000000000	0.35902200000000001000	0.99681958215911959000	0.69690700779794335000	0.00143143157903056780	2.02303146312248310000
2.00000000000000000000	0.48811700000000002000	0.99738566254287120000	0.69738173234070178000	0.00108652508990310960	2.02429147660805510000
2.00000000000000000000	0.67264100000000004000	0.99585989423632015000	0.69412881512579783000	-0.00287549702250277940	2.01811130911489260000
2.00000000000000000000	1.00000000000000000000	0.99585989423632015000	0.69412881512579783000	-0.00287549702250277940	2.01811130911489260000";
	}
}

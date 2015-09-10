/* @flow */

"use strict";

import * as xp_utils from './utils.js';
import {Parse} from 'parse';
import * as Database from './database.js';
import React from 'react';

export var Benchmark = Parse.Object.extend ('Benchmark');
export var Config = Parse.Object.extend ('Config');
export var Machine = Parse.Object.extend ('Machine');
export var Run = Parse.Object.extend ('Run');
export var RunSet = Parse.Object.extend ('RunSet');
export var PullRequest = Parse.Object.extend ('PullRequest');

export var xamarinColors = {
	//        light2     light1     normal     dark1      dark2
	"blue": [ "#91E2F4", "#4FCAE6", "#1FAECE", "#3192B3", "#2C7797" ],
	"teal": [ "#A3EBE1", "#7AD5C9", "#44B8A8", "#38A495", "#278E80" ],
	"green": [ "#CFEFA7", "#B3E770", "#91CA47", "#6FA22E", "#5A8622" ],
	"violet": [ "#CEC0EC", "#B5A1E0", "#9378CD", "#7E68C2", "#614CA0" ],
	"red": [ "#F8C6BB", "#F69781", "#F56D4F", "#E2553D", "#BC3C26" ],
	"amber": [ "#F7E28B", "#F9D33C", "#F1C40F", "#F0B240", "#E7963B" ],
	"gray": [ "#ECF0F1", "#D1D9DD", "#ADB7BE", "#9AA4AB", "#76828A" ],
	"asphalt": [ "#889DB5", "#66819E", "#365271", "#2B3E50", "#1C2B39" ]
};
export var xamarinColorsOrder = [ "blue", "green", "violet", "red", "asphalt", "amber", "gray", "teal" ];

export function start (started: () => void) {
	Parse.initialize('7khPUBga9c7L1YryD1se1bp6VRzKKJESc0baS9ES', 'qnBBT97Mttqsvq3g9zghnBVn2iiHLAQvTzekUigm');

	started ();
}

export class Controller {

	allBenchmarks: Array<Parse.Object>;
	allMachines: Array<Parse.Object>;
	allRunSets: Array<Parse.Object>;
	pullRequestRunSets: Array<Parse.Object>;
	allConfigs: Array<Parse.Object>;

	loadAsync () {

		pageParseQuery (() => new Parse.Query ((Machine : Parse.Object)),
			this.machinesLoaded.bind (this),
			function (error) {
				alert ("error loading machines: " + error.toString ());
			});

		pageParseQuery (() => new Parse.Query (RunSet)
				.notEqualTo ('failed', true)
				.include ('commit')
				.include ('pullRequest')
				.include ('pullRequest.baselineRunSet')
				.include ('pullRequest.baselineRunSet.commit'),
			this.runSetsLoaded.bind (this),
			function (error) {
				alert ("error loading run sets: " + error.toString ());
			});

		pageParseQuery (() => new Parse.Query (Config),
			this.configsLoaded.bind (this),
			function (error) {
				alert ("error loading configs: " + error.toString ());
			});

		pageParseQuery (() => new Parse.Query (Benchmark),
			results => {
				this.allBenchmarks = results;
				this.checkAllDataLoaded ();
			},
			function (error) {
				alert ("error loading benchmarks: " + error.toString ());
			});
	}

	allDataLoaded () {
	}

	machinesLoaded (results: Array<Parse.Object>) {
		this.allMachines = results;
		this.checkAllDataLoaded ();
	}

	configsLoaded (results: Array<Parse.Object>) {
		this.allConfigs = results;
		this.checkAllDataLoaded ();
	}

	runSetsLoaded (results: Array<Parse.Object>) {
		var partition = xp_utils.partitionArrayByString (results, rs => (rs.get ('pullRequest') === undefined).toString ());
		this.allRunSets = partition ["true"];
		this.pullRequestRunSets = partition ["false"];
		this.checkAllDataLoaded ();
	}

	allEnabledBenchmarks () : Array<Parse.Object> {
		return this.allBenchmarks.filter (b => !b.get ('disabled'));
	}

	benchmarkForId (id: string) : (Parse.Object | void) {
		for (var i = 0; i < this.allBenchmarks.length; ++i) {
			if (this.allBenchmarks [i].id === id)
				return this.allBenchmarks [i];
		}
		return undefined;
	}

	benchmarkNameForId (id: string) : string {
		var benchmark = this.benchmarkForId (id);
		if (benchmark === undefined)
			return "*unknown*";
		return benchmark.get ('name');
	}

	machineForId (id: string) : Parse.Object {
		var machine = xp_utils.find (this.allMachines, m => m.id === id);
		if (machine === undefined)
			throw "Machine not found";
		return machine;
	}

	configForId (id: string) : Parse.Object {
		var config = xp_utils.find (this.allConfigs, m => m.id === id);
		if (config === undefined)
			throw "Config not found";
		return config;
	}

	runSetForId (id: string) : Parse.Object {
		var runSet = xp_utils.find (this.allRunSets, rs => rs.id === id);
		if (runSet === undefined)
			throw "Run set not found";
		return runSet;
	}

	runSetsForMachineAndConfig (machine: Parse.Object, config: Parse.Object) : Array<Parse.Object> {
		return this.allRunSets.filter (
			rs =>
				rs.get ('machine').id === machine.id &&
				rs.get ('config').id === config.id
		);
	}

	runSetForPullRequestId (id: string) : Parse.Object {
		for (var i = 0; i < this.pullRequestRunSets.length; ++i) {
			var rs = this.pullRequestRunSets [i];
			var pr = rs.get ('pullRequest');
			if (pr.id === id)
				return rs;
		}
		throw "Run set for pull request not found";
	}

	checkAllDataLoaded () {
		if (this.allMachines === undefined
			|| this.allRunSets === undefined
			|| this.allBenchmarks === undefined
			|| this.allConfigs === undefined)
			return;

		this.allDataLoaded ();
	}

}

export function hashForRunSets (runSets: Array<Parse.Object>) : string {
	var ids = runSets.map (o => o.id);
	return ids.join ('+');
}

type ConfigDescriptionProps = {
	config: Parse.Object | void;
	omitHeader: boolean;
};

export class ConfigDescription extends React.Component<ConfigDescriptionProps, ConfigDescriptionProps, void> {
	render () : Object {
		var config = this.props.config;

		if (config === undefined)
			return <div></div>;

		var header = this.props.omitHeader
			? undefined
			: <h1>Config: {config.get ('name')}</h1>;
		var mono = config.get ('monoExecutable');
		var monoExecutable = mono === undefined
			? <span className="diagnostic">No mono executable specified.</span>
			: <code>{mono}</code>;
		var envVarsMap = config.get ('monoEnvironmentVariables') || {};
		var envVars = Object.keys (envVarsMap);
		var envVarsList = envVars.length === 0
			? <span className="diagnostic">No environment variables specified.</span>
			: <ul>
			{envVars.map (name => <li><code>{name + "=" + envVarsMap [name]}</code></li>)}
		</ul>;
		var options = config.get ('monoOptions') || [];
		var optionsList = options.length === 0
			? <span className="diagnostic">No command-line options specified.</span>
			: <code>{options.join (' ')}</code>;

		return <div className="Description">
			{header}
			<dl>
			<dt>Mono Executable</dt>
			<dd>{monoExecutable}</dd>
			<dt>Environment Variables</dt>
			<dd>{envVarsList}</dd>
			<dt>Command-line Options</dt>
			<dd>{optionsList}</dd>
			</dl>
		</div>;
	}
}

type MachineDescriptionProps = {
	machine: Parse.Object | void;
	omitHeader: boolean;
};

export class MachineDescription extends React.Component<MachineDescriptionProps, MachineDescriptionProps, void> {
	render () : Object {
		var machine = this.props.machine;

		if (machine === undefined)
			return <div></div>;
		var header = this.props.omitHeader
			? undefined
			: <h1>Machine: {machine.get ('name')}</h1>;

		return <div className="Description">
			{header}
			<dl>
			<dt>Architecture</dt>
			<dd>{machine.get ('architecture')}</dd>
			<dt>Dedicated?</dt>
			<dd>{machine.get ('isDedicated').toString ()}</dd>
			</dl>
		</div>;
	}
}

type MachineConfigSelection = {
	machine: string | void;
	config: string | void;
};

type ConfigSelectorProps = {
	runSetCounts: Array<{ machine: Database.DBObject, config: Database.DBObject, count: number }>;
	machine: string | void;
	config: string | void;
	onChange: (selection: MachineConfigSelection) => void;
};

export class CombinedConfigSelector extends React.Component<ConfigSelectorProps, ConfigSelectorProps, void> {
	render () : Object {
		var userStringForRSC = r => {
			return r.machine.get ('name') + " / " + r.config.get ('name');
		};

		var valueStringForEntry = (machine, config) => {
			return machine.get ('name') + '+' + config.get ('name');
		}

		var histogram = xp_utils.sortArrayLexicographicallyBy (this.props.runSetCounts, r => userStringForRSC (r).toLowerCase ());

		var machines = {};
		for (var i = 0; i < histogram.length; ++i) {
			var rsc = histogram [i];
			var machineName = rsc.machine.get ('name');
			machines [machineName] = machines [machineName] || [];
			machines [machineName].push (rsc);
		}

		function renderEntry (entry) {
			var string = valueStringForEntry (entry.machine, entry.config);
			return <option
				value={string}
				key={string}>
				{entry.config.get ('name')} ({entry.count})
			</option>;
		}

		function renderGroup (machines, machineName) {
			return <optgroup label={machineName}>
				{xp_utils.sortArrayNumericallyBy (machines [machineName], x => -x.count).map (renderEntry.bind (this))}
			</optgroup>;
		}

		var machine = this.props.machine;
		var config = this.props.config;
		var selectedValue = (machine === undefined || config === undefined) ? undefined : valueStringForEntry (machine, config);
		var aboutConfig = undefined;
		var aboutMachine = undefined;
		if (this.props.showControls) {
			aboutConfig = <button onClick={this.openConfigDescription.bind (this)}>About Config</button>;
			aboutMachine = <button onClick={this.openMachineDescription.bind (this)}>About Machine</button>;
		}
		return <div className="CombinedConfigSelector">
			<label>Machine &amp; Config</label>
			<select size="6" value={selectedValue} onChange={this.combinationSelected.bind (this)}>
				{Object.keys (machines).map (renderGroup.bind (this, machines))}
			</select>
			{aboutConfig}{aboutMachine}
			<div style={{ clear: 'both' }}></div>
		</div>;
	}

	openConfigDescription () {
        if (this.props.config === undefined)
            return;
        window.open ('config.html#' + this.props.config.get ('name'))
	}

	openMachineDescription () {
        if (this.props.machine === undefined)
            return;
        window.open ('machine.html#' + this.props.machine.get ('name'))
	}

	combinationSelected (event: Object) {
		var names = event.target.value.split ('+');
		var rsc = Database.findRunSetCount (this.props.runSetCounts, names [0], names [1]);
		this.props.onChange (rsc);
	}
}

type RunSetSelection = {
	machine: Parse.Object | void;
	config: Parse.Object | void;
	runSet: Parse.Object | void;
}

type RunSetSelectorProps = {
	controller: Controller;
	selection: RunSetSelection;
	onChange: (selection: RunSetSelection) => void;
};

export class RunSetSelector extends React.Component<RunSetSelectorProps, RunSetSelectorProps, void> {

	runSetSelected (event: Object) {
		var selection = this.props.selection;
		var runSetId = event.target.value;
		var runSet = this.props.controller.runSetForId (runSetId);
		this.props.onChange ({machine: selection.machine, config: selection.config, runSet: runSet});
	}

	configSelected (selection: MachineConfigSelection) {
		this.props.onChange ({machine: selection.machine, config: selection.config, runSet: undefined});
	}

	render () : Object {
		var selection = this.props.selection;

		var runSetId = undefined;
		var filteredRunSets = undefined;

		if (selection.runSet !== undefined)
			runSetId = selection.runSet.id;

		if (selection.machine !== undefined && selection.config !== undefined)
			filteredRunSets = this.props.controller.runSetsForMachineAndConfig (selection.machine, selection.config);

		function openRunSetDescription (id) {
			return window.open ('runset.html#' + id);
		}

		function renderRunSetOption (rs) {
			return <option value={rs.id} key={rs.id} onDoubleClick={openRunSetDescription.bind (this, rs.id)}>
				{rs.get ('startedAt').toString ()}
			</option>;
		}

		var config = selection.config === undefined
			? undefined
			: this.props.controller.configForId (selection.config.id);

		var configSelector =
			<CombinedConfigSelector
				controller={this.props.controller}
				machine={selection.machine}
				config={config}
				onChange={this.configSelected.bind (this)}
				showControls={true} />;
		var runSetsSelect = undefined;
		if (filteredRunSets === undefined) {
			runSetsSelect = <select size="6" disabled="true">
				<option className="diagnostic">Please select a machine and config.</option>
			</select>;
		} else if (filteredRunSets.length === 0) {
			runSetsSelect = <select size="6" disabled="true">
				<option className="diagnostic">No run sets found for this machine and config.</option>
			</select>;
		} else {
			runSetsSelect = <select
				size="6"
				selectedIndex="-1"
				value={runSetId}
				onChange={this.runSetSelected.bind (this)}>
				{filteredRunSets.map (renderRunSetOption)}
			</select>;
		}
		return <div className="RunSetSelector">
			{configSelector}
			<label>Run Set</label>
			{runSetsSelect}
			</div>;
	}
}

type RunSetDescriptionProps = {
	runSet: Parse.Object | void;
};

export class RunSetDescription extends React.Component<RunSetDescriptionProps, RunSetDescriptionProps, void> {
	constructor (props) {
		super (props);
		this.invalidateState (props.runSet);
	}

	invalidateState (runSet) {
		this.state = {};

		Database.fetch ('results?metric=eq.time&runset=eq.' + this.props.runSet.get ('id'), false,
		objs => {
			if (runSet !== this.props.runSet)
				return;
			this.setState ({results: objs})
		}, error => {
			alert ("error loading results: " + error.toString ());
		});
	}

	componentWillReceiveProps (nextProps) {
		this.invalidateState (nextProps.runSet);
	}

	render () {
		var runSet = this.props.runSet;
		var buildURL = runSet.get ('buildURL');
		var buildLink;
		var logURLs = runSet.get ('logURLs');
		var logLinks = [];
		var logLinkList;
		var timedOutBenchmarks;
		var crashedBenchmarks;
		var table;

		if (buildURL !== undefined)
			buildLink = <a href={buildURL}>build</a>;

		if (logURLs !== undefined && Object.keys (logURLs).length !== 0) {
			for (var key in logURLs) {
				var url = logURLs [key];
				var anchor = document.createElement ('a');
				anchor.href = url;

				var shortUrl = <span>{anchor.hostname}/&hellip;{anchor.pathname.substring (anchor.pathname.lastIndexOf ('/'))}</span>;
				logLinks.push(<li><a href={url}><code>{key}</code> ({shortUrl})</a></li>);
			}
			if (logLinks.length === 0) {
				logLinkList = undefined;
			} else {
				logLinkList = <ul>{logLinks}</ul>;
			}
		}

		if (this.state.results === undefined) {
			table = <div className='DiagnosticBlock'>Loading run data&hellip;</div>;
		} else {
			var resultsByBenchmark = {};
			for (var i = 0; i < this.state.results.length; ++i) {
				var result = this.state.results [i];
				resultsByBenchmark [result ['benchmark']] = { elapsed: result ['results'], disabled: result ['disabled'] };
			}
			var crashedBenchmarks = runSet.get ('crashedBenchmarks');
			var timedOutBenchmarks = runSet.get ('timedOutBenchmarks');
			var benchmarkNames = Object.keys (resultsByBenchmark);
			benchmarkNames.sort ();
			table = <table>
				<tr>
					<th>Benchmark</th>
					<th>Status</th>
					<th>Elapsed Times (ms)</th>
					<th>Bias due to Outliers</th>
				</tr>
				{benchmarkNames.map (name => {
					var result = resultsByBenchmark [name]
					var elapsed = result.elapsed;
					var disabled = result.disabled;
					elapsed.sort ();
					var elapsedString = elapsed.join (", ");
					var outlierVariance = xp_common.outlierVariance (elapsed);
					var statusIcons = [];
					if (crashedBenchmarks.indexOf (name) !== -1)
						statusIcons.push (<span className="statusIcon crashed fa fa-exclamation-circle" title="Crashed"></span>);
					if (timedOutBenchmarks.indexOf (name) !== -1)
						statusIcons.push (<span className="statusIcon timedOut fa fa-clock-o" title="Timed Out"></span>);
					if (statusIcons.length === 0)
						statusIcons.push (<span className="statusIcon good fa fa-check" title="Good"></span>);

					return <tr className={disabled ? 'disabled' : ''}>
						<td><code>{name}</code>{disabled ? ' (disabled)' : ''}</td>
						<td className="statusColumn">{statusIcons}</td>
						<td>{elapsedString}</td>
						<td>
							<div className="degree" title={outlierVariance}>
								<div className={outlierVariance}>&nbsp;</div>
							</div>
						</td>
					</tr>;
				})}
			</table>;
		}

		var commitHash = runSet.get ('commit');
		var commitLink = githubCommitLink (commitHash);

		return <div className="Description">
			<h1><a href={commitLink}>{commitHash.substring (0, 10)}</a> ({buildLink}, <a href={'compare.html#' + runSet.id}>compare</a>)</h1>
			{logLinkList}
			{table}
		</div>;
	}
}

export function githubCommitLink (commit: string) : string {
	return "https://github.com/mono/mono/commit/" + commit;
}

export function pageParseQuery (makeQuery: () => Object, success: (results: Array<Parse.Object>) => void, error: (error: Object) => void) : void {
    var limit = 1000;

    function done (results) {
		var values = [];
		for(var key in results) {
			var value = results [key];
			values.push (value);
		}
		success (values);
    }

    function page (skip, earliest, previousResults, soFar) {
		var query = makeQuery ();
		query.limit (limit);
		query.ascending ('createdAt');

		if (skip >= 10000) {
			skip = 0;
			earliest = previousResults [previousResults.length - 1].createdAt;
		}

		query.skip (skip);
		if (earliest !== undefined)
			query.greaterThanOrEqualTo ('createdAt', earliest);

		query.find ({
			success: function (results) {
				for (var i = 0; i < results.length; ++i)
					soFar [results [i].id] = results [i];
				if (results.length >= limit)
					page (skip + limit, earliest, results, soFar);
				else
					done (soFar);
			},
			error: function (e) {
				console.log ("Parse error: ", e);
				error (e);
			}
		});
    }

    page (0, undefined, [], {});
}

type NavigationProps = {
	currentPage: string;
}

export class Navigation extends React.Component<NavigationProps, NavigationProps, void> {

	render () : Object {
		var classFor = (page) =>
			this.props.currentPage === page ? 'selected' : 'deselected';
		return <div className="Navigation">
			<ul>
				<li
					title="View a timeline of all benchmarks"
					className={classFor ('timeline')}>
					<a href="index.html">Timeline</a>
				</li>
				<li
					title="Compare the results of multiple run sets"
					className={classFor ('compare')}>
					<a href="compare.html">Compare</a>
				</li>
			</ul>
		</div>;
	}

}

function minBy (f: (x: number) => number, x: number, y: number): number {
	return Math.min (f (x), f (y));
}

export function outlierVariance (samples: Array<number>): string {
	return computeOutlierVariance (
		jackknife (samples, computeMean),
		jackknife (samples, computeStandardDeviation),
		samples.length);
}

function computeMean (samples: Array<number>): number {
	return samples.reduce ((x, y) => x + y, 0) / samples.length;
}

function computeStandardDeviation (samples: Array<number>): number {
	var mean = computeMean (samples);
	var n = samples.length;
	return Math.sqrt (samples.reduce ((sum, x) => sum + (x - mean) * (x - mean), 0) / n);
}

function jackknife (samples: Array<number>, estimate: ((xs: Array<number>) => number)): object {
	var n = samples.length;
	var resampled = [];
	for (var i = 0; i < n; ++i)
		resampled.push (estimate (samples.slice (0, i).concat (samples.slice (i + 1))));
	return computeMean (resampled);
}

/* Given the bootstrap estimate of the mean (m) and of the standard deviation
 * (sb), as well as the original number of samples (n), computes the extent to
 * which outliers in the sample data affected the mean and standard deviation.
 */
function computeOutlierVariance (
	mean: number /* jackknife estimate of mean */,
	stdDev: number /* jackknife estimate of standard deviation */,
	n: number /* number of samples */
): string /* report */ {
	var variance = stdDev * stdDev;
	var mn = mean / n;
	var mgMin = mn / 2;
	var sampledStdDev = Math.min (mgMin / 4, stdDev / Math.sqrt (n));
	var sampledVariance = sampledStdDev * sampledStdDev;
	var outlierVariance = function (x) {
		var delta = n - x;
		return (delta / n) * (variance - delta * sampledVariance);
	}
	var cMax = function (x) {
		var k = mn - x;
		var d = k * k;
		var nd = n * d;
		var k0 = -n * nd;
		var k1 = variance - n * sampledVariance + nd;
		var det = k1 * k1 - 4 * sampledVariance * k0;
		return Math.floor (-2 * k0 / (k1 + Math.sqrt (det)));
	}
	var result = minBy (outlierVariance, 1, minBy (cMax, 0, mgMin)) / variance;
	if (isNaN (result))
		result = 0;
	return result < 0.01 ? 'none'
		: result < 0.10 ? 'slight'
		: result < 0.50 ? 'moderate'
		: 'severe';
}

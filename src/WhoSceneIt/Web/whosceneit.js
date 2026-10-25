define([], function () {
    'use strict';
    return function (view) {
        var find = function (name) { return view.querySelector('[data-id="' + name + '"]'); };
        if (!find('integrationSettings') || !find('deletePhase') || !find('allowUnwatched') || !find('deleteScope') || !find('retentionSettings')) {
            var notice = document.createElement('p'); notice.textContent = 'Who Scene It was updated. Reload this page to load the current deletion and synchronization controls.'; view.prepend(notice); return;
        }
        var state = { mode: 'user', start: 0, total: 0, titleStart: 0, selected: null, series: null, token: 0, titleToken: 0, active: false, initialized: false, busy: false, exporting: false, reportHealthy: false };
        var pollTimer = null, filterTimer = null, deletionTimer = null;
        var selections = new Set(), selectionContext = '', currentRows = [];
        var integrationsLoaded = false, integrationBusy = false;
        var retention = { settings:null, actor:null, flags:{}, editing:null, busy:false }, retentionTimer = null;
        var deletion = { token: null, state: null, busy: false, submitting: false, ready: 0, request: 0, ids: [], userId: null, allowUnwatched: false };
        var pageSize = 100;
        function api(path, data, method) {
            return ApiClient.ajax({ type: method || 'GET', url: ApiClient.getUrl('WhoSceneIt/' + path, data || {}), dataType: 'json' }).catch(normalizeError);
        }
        function normalizeError(error) {
            if (!error || error.responseJSON || error.responseText || typeof error.text !== 'function') return Promise.reject(error);
            var response;
            try { response = typeof error.clone === 'function' ? error.clone() : error; } catch (_) { return Promise.reject(error); }
            return Promise.resolve().then(function () { return response.text(); }).then(function (body) {
                var normalized = { status: error.status, responseText: body };
                try { normalized.responseJSON = JSON.parse(body); } catch (_) {}
                return Promise.reject(normalized);
            }, function () { return Promise.reject(error); });
        }
        function text(node, value) { node.textContent = value == null ? '' : String(value); }
        function errorText(error) {
            if (error && (error.status === 401 || error.status === 403)) return 'Administrator access is required. Sign in again with an administrator account.';
            var result = error && error.responseJSON;
            if (!result && error && error.responseText) { try { result = JSON.parse(error.responseText); } catch (_) {} }
            var response = result && (result.ResponseStatus || result.responseStatus);
            var message = response && (response.Message || response.message) || result && (result.Message || result.message);
            var code = error && error.status ? ' (HTTP ' + error.status + ')' : '';
            if (typeof message === 'string' && message.trim()) return 'Emby request failed' + code + ': ' + message.trim().slice(0, 1200);
            return 'The request failed' + code + '. Check your connection and Emby Server logs, then try again.';
        }
        function status(message, error) { text(find('reportStatus'), message); find('reportStatus').classList.toggle('wsi-error', !!error); }
        function deletionApi(action, data, method) {
            if (method === 'GET') return api('Delete/' + action, data);
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('WhoSceneIt/Delete/' + action), data: JSON.stringify(data || {}), contentType: 'application/json', dataType: 'json' }).catch(normalizeError);
        }
        function integrationApi(path, data) {
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('WhoSceneIt/Integrations' + path), data: JSON.stringify(data), contentType: 'application/json', dataType: 'json' }).catch(normalizeError);
        }
        function retentionApi(path, data) {
            return ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('WhoSceneIt/Retention/' + path), data: JSON.stringify(data || {}), contentType: 'application/json', dataType: 'json' }).catch(normalizeError);
        }
        function cleanupControls() {
            var automatic = find('cleanupEnabled').checked && find('cleanupAction').value === 'Automatic';
            find('cleanupAuthorizeField').hidden = !automatic;
            find('saveCleanup').disabled = !retention.settings || retention.busy || (automatic && !find('cleanupAuthorize').checked);
            find('evaluateCleanup').disabled = !retention.editing || retention.busy;
            find('removeCleanup').disabled = !retention.editing || retention.busy;
            find('cleanupIntervalField').hidden = find('cleanupSchedule').value !== 'Interval';
            find('cleanupTimeField').hidden = ['Daily','Weekly'].indexOf(find('cleanupSchedule').value) < 0;
            find('cleanupWeekdayField').hidden = find('cleanupSchedule').value !== 'Weekly';
            find('clearCleanupHold').disabled = retention.busy || !find('cleanupHoldList').value || !find('cleanupHoldAuthorize').checked;
            find('removeProtection').disabled = !find('protectedList').value || retention.busy;
            updateSelections();
        }
        function editCleanup(id) {
            var p = retention.settings && retention.settings.Policies.find(function (p) { return p.Id === id; });
            retention.editing = p ? p.Id : null;
            find('cleanupRule').value = p ? p.Id : '';
            var defaults = { Name:'Watched media cleanup', UserId:find('userSelect').value, Scope:'Both', WatchMode:'Watched', MinimumDays:7, MaximumItems:100, Action:'Review', Schedule:'Off', IntervalHours:24, TimeUtc:'03:00', Weekday:0 };
            [['cleanupName','Name'],['cleanupUser','UserId'],['cleanupScope','Scope'],['cleanupWatchMode','WatchMode'],['cleanupDays','MinimumDays'],['cleanupMaximum','MaximumItems'],['cleanupAction','Action'],['cleanupSchedule','Schedule'],['cleanupHours','IntervalHours'],['cleanupTime','TimeUtc'],['cleanupWeekday','Weekday']].forEach(function (pair) { find(pair[0]).value = p ? p[pair[1]] : defaults[pair[1]]; });
            [['cleanupEnabled','Enabled'],['cleanupPlayback','OnPlaybackStopped'],['cleanupWatched','OnWatchedChanged'],['cleanupStartup','OnStartup'],['cleanupDashboard','OnDashboard']].forEach(function (pair) { find(pair[0]).checked = !!(p && p[pair[1]]); });
            find('cleanupAuthorize').checked = false;
            text(find('cleanupNext'), p ? 'Next scheduled evaluation: ' + dateLabel(p.NextRunUtc) + '. Event rules also observe the settling delay and cooldown.' : 'New rules are disabled and prompt for review by default.');
            cleanupControls();
        }
        function renderRetention(result) {
            retention.settings = result.Settings;
            if (result.ActorId) retention.actor = result.ActorId;
            if (!retention.settings) throw new Error('Cleanup settings were not returned.');
            var selected = retention.editing || '', protection = find('protectedList').value;
            find('cleanupRule').replaceChildren(); var option = document.createElement('option'); option.value = ''; text(option, 'New rule'); find('cleanupRule').appendChild(option);
            (retention.settings.Policies || []).filter(function (p) { return p.OwnerId === retention.actor; }).forEach(function (p) { var o = document.createElement('option'); o.value = p.Id; text(o, p.Name + (p.Enabled ? ' (enabled)' : ' (disabled)')); find('cleanupRule').appendChild(o); });
            find('cleanupRule').value = selected;
            find('protectedList').replaceChildren(); option = document.createElement('option'); option.value = ''; text(option, 'Choose a protected title (' + retention.settings.Protected.length + ')'); find('protectedList').appendChild(option);
            retention.settings.Protected.forEach(function (p) { var o = document.createElement('option'); o.value = p.ItemId; text(o, p.Name + ' (' + p.Type + ')'); find('protectedList').appendChild(o); }); find('protectedList').value = protection;
            var reviews = retention.settings.Reviews.filter(function (r) { return r.OwnerId === retention.actor; });
            find('cleanupPrompt').hidden = !reviews.length; text(find('cleanupPrompt'), reviews.length + ' cleanup prompt(s) await review. Open Never delete and automatic cleanup to review them. No deletion starts from a prompt alone.');
            find('cleanupReviews').replaceChildren();
            if (!reviews.length) text(find('cleanupReviews'), 'No cleanup prompts await this administrator.');
            reviews.forEach(function (r) { var line = document.createElement('p'); text(line, r.Name + ': ' + r.ItemIds.length + ' item(s), ' + r.Trigger + '. '); var review = document.createElement('button'); review.type = 'button'; text(review, 'Review media'); review.dataset.cleanupReview = r.Id; review.addEventListener('click', function () { openCleanupReview(r.Id); }); line.appendChild(review); var dismiss = document.createElement('button'); dismiss.type = 'button'; text(dismiss, 'Dismiss'); dismiss.addEventListener('click', function () { retentionAction('Dismiss', { Id:r.Id }); }); line.appendChild(dismiss); find('cleanupReviews').appendChild(line); });
            find('cleanupRuns').replaceChildren();
            retention.settings.Runs.slice().reverse().slice(0,12).forEach(function (r) { var line = document.createElement('p'); text(line, r.Name + ': ' + r.State + ', ' + (r.Deleted || 0) + ' deleted, ' + (r.Failed || 0) + ' failed, ' + (r.Skipped || 0) + ' skipped, ' + (r.SyncFailed || 0) + ' service issues. ' + (r.Message || '') + ' ' + dateLabel(r.StartedUtc)); if (r.Token && r.OwnerId === retention.actor && (r.State === 'Deleting' || r.FinishedUtc && Date.parse(r.FinishedUtc) + 600000 > Date.now())) { var progress = document.createElement('button'); progress.type = 'button'; text(progress, r.State === 'Deleting' ? 'View progress / stop' : 'View details'); progress.addEventListener('click', function () { if (deletion.busy && deletion.token !== r.Token) { text(find('cleanupStatus'), 'Use the current deletion dialog before opening another operation.'); return; } deletion.token = r.Token; deletion.busy = r.State === 'Deleting'; deletion.state = r.State; find('deleteAcknowledge').checked = false; if (!find('deleteDialog').open) find('deleteDialog').showModal(); pollDeletion(); }); line.appendChild(progress); } find('cleanupRuns').appendChild(line); });
            if (!retention.settings.Runs.length) text(find('cleanupRuns'), 'No cleanup runs yet.');
            find('cleanupHoldFields').hidden = !retention.settings.Holds.length;
            var selectedHold = find('cleanupHoldList').value; find('cleanupHoldList').replaceChildren(); var holdOption = document.createElement('option'); holdOption.value = ''; text(holdOption,'Choose an automation hold'); find('cleanupHoldList').appendChild(holdOption);
            retention.settings.Holds.forEach(function (h) { var o = document.createElement('option'); o.value = h.RunId + '/' + h.ItemId; text(o, h.Name + ' (item ' + h.ItemId + ')'); find('cleanupHoldList').appendChild(o); }); find('cleanupHoldList').value = selectedHold; if (!find('cleanupHoldList').value) find('cleanupHoldAuthorize').checked = false;
            text(find('cleanupHolds'), retention.settings.Holds.length ? retention.settings.Holds.length + ' item(s) are on automation hold after an uncertain or failed operation. Inspect them in Emby and the configured service. They are excluded from future automatic runs; use manual deletion after review, or explicitly clear a reviewed hold to allow a future fresh cleanup. Holds persist across restarts.' : 'No uncertain cleanup attempts are on hold. Confirmed jobs are never replayed after a restart.');
            text(find('cleanupStatus'), result.Message || 'Cleanup settings loaded.');
            if (retention.editing && !retention.settings.Policies.some(function (p) { return p.Id === retention.editing; })) editCleanup(null);
            cleanupControls();
        }
        function loadRetention() {
            if (!state.active) return Promise.resolve();
            clearTimeout(retentionTimer);
            return api('Retention').then(function (result) { if (state.active) renderRetention(result); }).catch(function (error) { text(find('cleanupStatus'), errorText(error)); }).then(function () { if (state.active) retentionTimer = setTimeout(loadRetention,15000); });
        }
        function retentionAction(path, data) {
            if (retention.busy) return Promise.resolve(); retention.busy = true; cleanupControls();
            return retentionApi(path,data).then(function () { return loadRetention(); }).catch(function (error) { text(find('cleanupStatus'),errorText(error)); }).then(function () { retention.busy = false; cleanupControls(); });
        }
        function saveCleanup(event) {
            event.preventDefault(); if (find('saveCleanup').disabled) return;
            var p = { Id:retention.editing, Name:find('cleanupName').value.trim(), UserId:find('cleanupUser').value, Scope:find('cleanupScope').value, WatchMode:find('cleanupWatchMode').value, MinimumDays:Number(find('cleanupDays').value), MaximumItems:Number(find('cleanupMaximum').value), Action:find('cleanupAction').value, Schedule:find('cleanupSchedule').value, IntervalHours:Number(find('cleanupHours').value), TimeUtc:find('cleanupTime').value, Weekday:Number(find('cleanupWeekday').value), Enabled:find('cleanupEnabled').checked, OnPlaybackStopped:find('cleanupPlayback').checked, OnWatchedChanged:find('cleanupWatched').checked, OnStartup:find('cleanupStartup').checked, OnDashboard:find('cleanupDashboard').checked };
            retention.busy = true; cleanupControls();
            retentionApi('Policy', { Policy:p, Confirmed:find('cleanupAuthorize').checked }).then(function (saved) { retention.editing = saved.Id; find('cleanupAuthorize').checked = false; return loadRetention().then(function () { editCleanup(saved.Id); }); }).catch(function (error) { text(find('cleanupStatus'), errorText(error)); }).then(function () { retention.busy = false; cleanupControls(); });
        }
        function protectMedia(ids, protect) {
            if (!ids.length || retention.busy) return; retention.busy = true; cleanupControls();
            retentionApi('Protection', { ItemIds:ids.join(','), Protected:protect }).then(function (result) { renderRetention(result); selections.clear(); return loadReport(); }).catch(function (error) { text(find('cleanupStatus'),errorText(error)); status(errorText(error),true); }).then(function () { retention.busy = false; cleanupControls(); });
        }
        function protectionButton(id) {
            var flag = retention.flags[id] || {}, button = document.createElement('button'); button.type = 'button'; button.dataset.protectItem = id;
            text(button, flag.NeverDelete ? flag.Explicit ? 'Remove never delete' : 'Protected by show' : 'Never delete'); button.disabled = !!flag.NeverDelete && !flag.Explicit; button.title = flag.Reason || 'Protect this title from every deletion mode';
            button.addEventListener('click', function () { protectMedia([flag.SourceId || id], !flag.NeverDelete); }); return button;
        }
        function openCleanupReview(id) {
            if (deletion.busy) return;
            var request = ++deletion.request; deletion.reviewId = id; deletion.ids = []; deletion.state = 'Loading'; deletion.token = null; deletion.ready = 0;
            find('deleteAcknowledge').checked = false; find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('retryPreview').hidden = true; find('closeDelete').hidden = true; find('cancelDelete').hidden = false; find('cancelDelete').disabled = false; text(find('cancelDelete'),'Cancel'); find('deleteProgressArea').hidden = true; find('deleteItems').replaceChildren();
            text(find('deletePhase'),'Preparing cleanup review. No deletion has started.'); text(find('deleteScope'),'Rechecking the saved rule and its selected items.'); text(find('deleteSummary'),'Checking protection, completed state, permissions, playback, paths, and configured services...');
            if (!find('deleteDialog').open) find('deleteDialog').showModal();
            retentionApi('Review',{Id:id}).then(function (result) { if (request !== deletion.request || !state.active) { if (result.Token) deletionApi('Cancel',{Token:result.Token}).catch(function () {}); return; } showDeletion(result); }).catch(function (error) { if (request === deletion.request) previewFailed(deletionError(error)); });
        }

        function integrationControls() {
            ['saveIntegrations','testSonarr','testRadarr'].forEach(function (id) { find(id).disabled = !integrationsLoaded || integrationBusy || deletion.busy; });
            find('reloadIntegrations').disabled = integrationBusy || deletion.busy;
            view.querySelectorAll('[data-arr-input]').forEach(function (input) { input.disabled = !integrationsLoaded || integrationBusy || deletion.busy; });
        }
        function loadIntegrations() {
            if (integrationBusy || deletion.busy) return;
            integrationBusy = true; integrationControls();
            api('Integrations').then(renderIntegrations).catch(function (error) { text(find('integrationStatus'), errorText(error)); })
                .then(function () { integrationBusy = false; integrationControls(); });
        }
        function renderIntegrations(settings) {
            ['sonarr','radarr'].forEach(function (name) {
                var c = settings[name === 'sonarr' ? 'Sonarr' : 'Radarr'];
                find(name + 'Enabled').checked = !!c.Enabled; find(name + 'Url').value = c.BaseUrl || '';
                find(name + 'Key').value = ''; find(name + 'ClearKey').checked = false;
                find(name + 'EmbyRoot').value = c.EmbyPathRoot || ''; find(name + 'ArrRoot').value = c.ArrPathRoot || '';
                text(find(name + 'KeyStatus'), c.HasApiKey ? 'A key is stored. Leave blank to keep it.' : 'No API key is stored.');
            });
            find('radarrExclude').checked = !!settings.Radarr.AddImportExclusion; find('sonarrRemoveEmpty').checked = !!settings.Sonarr.RemoveEmptySeries;
            var enabled = ['Sonarr','Radarr'].filter(function (name) { return settings[name].Enabled; });
            text(find('integrationSummary'), enabled.length ? 'Synchronization enabled for ' + enabled.join(' and ') + '. Unmatched or unreachable items are blocked before media deletion.' : 'Emby-only deletion. Configure and enable Sonarr/Radarr to synchronize their records.');
            integrationsLoaded = true; integrationControls();
        }
        function connectionValues(name) {
            return { Enabled: find(name + 'Enabled').checked, BaseUrl: find(name + 'Url').value.trim(), ApiKey: find(name + 'Key').value,
                ClearApiKey: find(name + 'ClearKey').checked, EmbyPathRoot: find(name + 'EmbyRoot').value.trim(), ArrPathRoot: find(name + 'ArrRoot').value.trim(), AddImportExclusion: name === 'radarr' && find('radarrExclude').checked, RemoveEmptySeries:name === 'sonarr' && find('sonarrRemoveEmpty').checked };
        }
        function saveIntegrations() {
            if (integrationBusy || deletion.busy) return;
            integrationBusy = true; integrationControls(); text(find('integrationStatus'), 'Saving synchronization settings...');
            integrationApi('', { Sonarr: connectionValues('sonarr'), Radarr: connectionValues('radarr') }).then(function (result) {
                renderIntegrations(result); text(find('integrationStatus'), 'Settings saved. Prepare a new deletion preview to use them.');
            }).catch(function (error) { text(find('integrationStatus'), errorText(error)); }).then(function () { integrationBusy = false; integrationControls(); });
        }
        function testIntegration(name) {
            if (integrationBusy || deletion.busy) return;
            integrationBusy = true; integrationControls(); text(find('integrationStatus'), 'Testing ' + name + ' connection without changing media or saving settings...');
            integrationApi('/Test', { Service: name, Connection: connectionValues(name.toLowerCase()) }).then(function (result) { text(find('integrationStatus'), result.Message); })
                .catch(function (error) { text(find('integrationStatus'), errorText(error)); }).then(function () { integrationBusy = false; integrationControls(); });
        }
        function deletionError(error) {
            return errorText(error);
        }
        function updateSelections() {
            integrationControls();
            var byTitle = state.mode === 'title';
            var allStates = find('allowUnwatched').checked;
            find('allowUnwatched').disabled = deletion.busy;
            find('viewDeletion').hidden = !deletion.token || deletion.state === 'Ready' || deletion.state === 'Loading' || deletion.state === 'PreviewFailed';
            text(find('viewDeletion'), deletion.busy ? 'View deletion progress' : 'View deletion results');
            text(find('selectionCount'), selections.size + ' selected');
            find('selectionCount').hidden = byTitle; find('clearSelection').hidden = byTitle;
            find('deleteSelected').hidden = byTitle; find('deleteTitle').hidden = !byTitle;
            find('clearSelection').disabled = !selections.size || deletion.busy;
            find('deleteSelected').disabled = !selections.size || state.busy || deletion.busy || retention.busy;
            find('protectSelected').disabled = !selections.size || retention.busy || state.busy; find('protectSelected').hidden = byTitle; find('protectTitle').hidden = !byTitle || !state.selected;
            var titleFlag = state.selected && retention.flags[state.selected.Id] || {}; text(find('protectTitle'),titleFlag.NeverDelete ? titleFlag.Explicit ? 'Remove never delete' : 'Protected by show' : 'Never delete this title'); find('protectTitle').disabled = state.busy || retention.busy || !!titleFlag.NeverDelete && !titleFlag.Explicit;
            find('deleteTitle').disabled = !!titleFlag.NeverDelete || !state.selected || !state.reportHealthy || (!allStates && !currentRows.some(canDelete)) || state.busy || deletion.busy || retention.busy;
            text(find('deleteTitle'), allStates ? 'Delete selected title media...' : 'Delete watched media...');
            text(find('selectionNote'), allStates ? 'All watch states are eligible for review. Selected shows include all available episodes. Media with saved progress for any user can be removed. Deleting media affects every user; review the exact files and service actions.' : byTitle ? 'Deletion includes completed media only. Saved partial playback and rewatches for any user are preserved. Review the exact paths before confirming.' : 'Select fully watched media individually or by page. Selections carry across pages. TV shows expand to completed episodes. Saved partial playback and rewatches for any user are preserved. Deleting media affects every user.');
            view.querySelectorAll('[data-item-selection]').forEach(function (box) { box.checked = selections.has(box.dataset.itemSelection); box.disabled = deletion.busy || state.busy || retention.busy; });
            view.querySelectorAll('[data-delete-item]').forEach(function (button) { var row = currentRows.find(function (r) { return r.ItemId === button.dataset.deleteItem; }); button.disabled = state.busy || deletion.busy || retention.busy || !row || !canDelete(row); });
            view.querySelectorAll('[data-protect-item]').forEach(function (button) { var flag = retention.flags[button.dataset.protectItem] || {}; button.disabled = state.busy || retention.busy || !!flag.NeverDelete && !flag.Explicit; });
            var pageBox = view.querySelector('[data-page-selection]');
            if (pageBox) {
                var eligible = currentRows.filter(canDelete);
                pageBox.checked = !!eligible.length && eligible.every(function (row) { return selections.has(row.ItemId); });
                pageBox.indeterminate = !pageBox.checked && eligible.some(function (row) { return selections.has(row.ItemId); });
                pageBox.disabled = deletion.busy || state.busy || retention.busy || !eligible.length;
            }
        }
        function canDelete(row) { if ((retention.flags[row.ItemId] || {}).NeverDelete) return false; return find('allowUnwatched').checked ? ['Movie','Episode','Series'].indexOf(row.Type) >= 0 : row.DeletableCount != null ? row.DeletableCount > 0 : row.WatchedCount > 0 && !row.WatchedWithResumeCount; }
        function resetSelections() { selections.clear(); updateSelections(); }
        function reportParams(start, limit) {
            var values = { StartIndex: start, Limit: limit, WatchState: find('watchState').value, IncludeInProgress: true, SearchTerm: find('filter').value.trim(), SortBy: find('sort').value };
            if (state.mode === 'user') { values.UserId = find('userSelect').value; values.View = find('viewSelect').value; if (state.series) values.SeriesId = state.series.Id; }
            else values.ItemId = state.selected.Id;
            return values;
        }
        function changeMode(mode) {
            state.mode = mode; state.start = 0; state.token++;
            find('userPanel').hidden = mode !== 'user'; find('titlePanel').hidden = mode !== 'title';
            find('userTab').setAttribute('aria-selected', String(mode === 'user'));
            find('titleTab').setAttribute('aria-selected', String(mode === 'title'));
            clearReport(); loadReport();
        }
        function clearReport() {
            state.total = 0; find('reportBody').replaceChildren(); find('tableWrap').hidden = true;
            find('pager').hidden = true; find('export').disabled = true;
            currentRows = []; updateSelections();
        }
        function cell(row, value, small) {
            var td = document.createElement('td'); text(td, value);
            if (small) { var detail = document.createElement('small'); text(detail, small); td.appendChild(detail); }
            row.appendChild(td); return td;
        }
        function dateLabel(value) {
            if (!value) return 'Not recorded';
            var date = new Date(value); return isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleString();
        }
        function durationLabel(ticks) {
            if (ticks == null) return '';
            var seconds = Math.floor(Math.max(0, ticks) / 10000000), hours = Math.floor(seconds / 3600), minutes = Math.floor(seconds / 60) % 60;
            return (hours ? hours + ':' + String(minutes).padStart(2, '0') : String(minutes)) + ':' + String(seconds % 60).padStart(2, '0');
        }
        function progressCell(row, item) {
            var series = item.Type === 'Series', percent = series ? item.CompletionPercent : item.PlaybackPercent;
            var td = cell(row, percent == null ? (series ? 'Episode total unavailable' : (item.PlaybackPositionTicks > 0 ? 'Resume at ' + durationLabel(item.PlaybackPositionTicks) : 'Duration not recorded')) : percent + '%' + (series ? ' episodes completed' : (item.WatchedCount && !item.WatchedWithResumeCount ? ' complete' : ' saved position')));
            td.className = 'wsi-progress';
            if (percent != null) { var bar = document.createElement('progress'); bar.max = 100; bar.value = Math.max(0, Math.min(100, percent)); bar.setAttribute('aria-label', series ? 'Episodes completed' : 'Saved playback position'); td.appendChild(bar); }
            var detail = document.createElement('small');
            if (series) text(detail, (item.NotStartedCount == null ? '' : item.NotStartedCount + ' not started') + (item.WatchedWithResumeCount ? '; ' + item.WatchedWithResumeCount + ' watched with resume' : ''));
            else if (item.RunTimeTicks != null) text(detail, item.WatchedCount && !item.WatchedWithResumeCount ? 'Completed in Emby; ' + durationLabel(item.RunTimeTicks) + ' total' : durationLabel(item.PlaybackPositionTicks) + ' of ' + durationLabel(item.RunTimeTicks) + '; ' + durationLabel(item.RemainingTicks) + ' remaining');
            else text(detail, 'Duration not recorded; percentage and remaining time unavailable');
            td.appendChild(detail);
        }
        function episodeButton(parent, item, byTitle) {
            var detail = document.createElement('small'), button = document.createElement('button'); button.type = 'button'; button.className = 'wsi-link'; text(button, 'View episodes');
            button.setAttribute('aria-label', 'View episodes for ' + (byTitle ? item.UserName : item.Name));
            button.addEventListener('click', function () {
                state.series = byTitle ? { Id: state.selected.Id, Name: state.selected.Name } : { Id: item.ItemId, Name: item.Name };
                if (byTitle) find('userSelect').value = item.UserId;
                find('viewSelect').value = 'Episodes'; find('sort').value = 'Episode'; find('filter').value = '';
                text(find('episodeScopeName'), 'Episodes in ' + state.series.Name); find('episodeScope').hidden = false; changeMode('user');
            }); detail.appendChild(button); parent.appendChild(detail);
        }
        function renderReport(result) {
            var rows = result.Items || [], byTitle = state.mode === 'title';
            state.total = result.TotalRecordCount || 0;
            currentRows = rows;
            var headings = byTitle ? ['User', 'Watch status', 'Watched / partial', 'Progress', 'Saved play count', 'Last played'] : ['Title', 'Type', 'Watch status', 'Watched / partial', 'Progress', 'Saved play count', 'Last played'];
            var tr = document.createElement('tr');
            if (!byTitle) {
                var selectionHead = document.createElement('th'), pageBox = document.createElement('input');
                pageBox.type = 'checkbox'; pageBox.dataset.pageSelection = 'true'; pageBox.setAttribute('aria-label', 'Select watched titles on this page');
                pageBox.addEventListener('change', function () { currentRows.filter(canDelete).forEach(function (row) { if (pageBox.checked) selections.add(row.ItemId); else selections.delete(row.ItemId); }); updateSelections(); });
                selectionHead.appendChild(pageBox); tr.appendChild(selectionHead);
            }
            headings.forEach(function (label) { var th = document.createElement('th'); th.scope = 'col'; text(th, label); tr.appendChild(th); });
            if (!byTitle) { var actionHead = document.createElement('th'); actionHead.scope = 'col'; text(actionHead, 'Media actions'); tr.appendChild(actionHead); }
            find('reportHead').replaceChildren(tr); find('reportBody').replaceChildren();
            rows.forEach(function (item) {
                var row = document.createElement('tr');
                if (!byTitle) {
                    var selectionCell = cell(row, ''); selectionCell.className = 'wsi-selection';
                    if (canDelete(item)) {
                        var box = document.createElement('input'); box.type = 'checkbox'; box.dataset.itemSelection = item.ItemId;
                        box.setAttribute('aria-label', 'Select ' + item.Name);
                        box.addEventListener('change', function () { if (box.checked) selections.add(item.ItemId); else selections.delete(item.ItemId); updateSelections(); });
                        selectionCell.appendChild(box);
                    }
                }
                if (byTitle) { var userCell = cell(row, item.UserName, item.UserDisabled ? 'Disabled account' : ''); if (item.Type === 'Series') episodeButton(userCell, item, true); }
                else {
                    var nameCell = cell(row, '', item.Type === 'Episode' ? [item.SeriesName, item.Episode].filter(Boolean).join(' - ') : (item.Year || ''));
                    var titleButton = document.createElement('button'); titleButton.type = 'button'; titleButton.className = 'wsi-link'; text(titleButton, item.Name);
                    titleButton.addEventListener('click', function () { state.selected = { Id: item.ItemId, Name: item.Name, Type: item.Type, Year: item.Year }; showSelected(); changeMode('title'); });
                    nameCell.prepend(titleButton); if (item.Type === 'Series') episodeButton(nameCell, item, false); cell(row, item.Type === 'Series' ? 'TV show' : item.Type);
                }
                cell(row, item.Status);
                var count = item.WatchedCount + ' watched';
                if (item.Type === 'Series') count += ' episodes';
                if (item.AvailableEpisodeCount != null) count += ' of ' + item.AvailableEpisodeCount + ' available';
                if (item.InProgressCount) count += '; ' + item.InProgressCount + ' partially watched';
                cell(row, count); progressCell(row, item); cell(row, item.SavedPlayCount || 0); cell(row, dateLabel(item.LastPlayedUtc));
                if (!byTitle) {
                    var actionCell = cell(row, ''), remove = document.createElement('button'); remove.type = 'button'; remove.className = 'wsi-danger';
                    text(remove, item.Type === 'Series' ? (find('allowUnwatched').checked ? 'Delete episodes...' : 'Delete watched episodes...') : 'Delete...'); remove.disabled = !canDelete(item) || deletion.busy;
                    remove.dataset.deleteItem = item.ItemId; remove.setAttribute('aria-label', 'Review deletion of ' + item.Name);
                    remove.addEventListener('click', function () { previewDeletion([item.ItemId]); }); actionCell.appendChild(remove); actionCell.appendChild(protectionButton(item.ItemId));
                }
                find('reportBody').appendChild(row);
            });
            find('tableWrap').hidden = !rows.length; find('pager').hidden = state.total <= pageSize;
            find('prev').disabled = state.start <= 0; find('next').disabled = state.start + rows.length >= state.total;
            text(find('pageInfo'), (state.start + 1) + '-' + (state.start + rows.length) + ' of ' + state.total);
            find('export').disabled = !state.total || state.exporting;
            status(state.total ? state.total + ' matching ' + (byTitle ? 'users' : 'titles') + '. Dates use your browser time zone.' : 'No matching results.');
            updateSelections();
        }
        function loadReport() {
            if (!state.active) return Promise.resolve();
            if (state.mode === 'title' && !state.selected) { clearReport(); status('Search for a title and select a result.'); return Promise.resolve(); }
            if (state.mode === 'user' && !find('userSelect').value) { clearReport(); status('No Emby users were found.'); return Promise.resolve(); }
            var token = ++state.token, path = state.mode === 'user' ? 'UserReport' : 'Viewers';
            var context = JSON.stringify([state.mode, find('userSelect').value, find('viewSelect').value, find('filter').value.trim(), find('watchState').value, find('allowUnwatched').checked, state.series && state.series.Id, state.selected && state.selected.Id]);
            if (context !== selectionContext) { selectionContext = context; selections.clear(); }
            state.busy = true; state.reportHealthy = false; find('export').disabled = true; status('Loading report...');
            updateSelections();
            return api(path, reportParams(state.start, pageSize)).then(function (result) {
                if (token !== state.token || !state.active) return;
                var ids = Array.from(new Set((result.Items || []).map(function (row) { return row.ItemId; }).concat(state.mode === 'title' && state.selected ? [state.selected.Id] : [])));
                return (ids.length ? api('Retention/Protection',{ItemIds:ids.join(',')}) : Promise.resolve([])).then(function (flags) {
                if (token !== state.token || !state.active) return;
                retention.flags = {}; flags.forEach(function (flag) { retention.flags[flag.ItemId] = flag; if (flag.NeverDelete) selections.delete(flag.ItemId); });
                state.busy = false;
                state.reportHealthy = true;
                if (!result.Items.length && state.start > 0 && result.TotalRecordCount > 0) { state.start = Math.floor((result.TotalRecordCount - 1) / pageSize) * pageSize; return loadReport(); }
                renderReport(result);
                });
            }).catch(function (error) { if (token === state.token && state.active) { state.busy = false; state.reportHealthy = false; selections.clear(); clearReport(); status(errorText(error), true); } });
        }
        function showSelected() {
            find('selectedTitle').hidden = !state.selected;
            if (state.selected) text(find('selectedTitle'), 'Selected: ' + state.selected.Name + (state.selected.Year ? ' (' + state.selected.Year + ')' : '') + ' - ' + (state.selected.Type === 'Series' ? 'TV show' : state.selected.Type));
        }
        function searchTitles(start) {
            var term = find('titleSearch').value.trim(), token = ++state.titleToken;
            if (!term) return;
            state.titleStart = start; text(find('titleResults'), 'Searching...'); find('titlePager').hidden = true;
            api('Titles', { SearchTerm: term, Type: find('titleType').value, StartIndex: start, Limit: 30 }).then(function (result) {
                if (token !== state.titleToken || !state.active) return;
                find('titleResults').replaceChildren();
                result.Items.forEach(function (item) {
                    var button = document.createElement('button'); button.type = 'button';
                    text(button, item.Name + (item.Year ? ' (' + item.Year + ')' : ''));
                    var detail = document.createElement('small'); text(detail, [item.Type === 'Series' ? 'TV show' : item.Type, item.SeriesName, item.Id].filter(Boolean).join(' - ')); button.appendChild(detail);
                    button.addEventListener('click', function () { state.selected = item; state.start = 0; showSelected(); loadReport(); });
                    find('titleResults').appendChild(button);
                });
                if (!result.Items.length) text(find('titleResults'), 'No titles found.');
                find('titlePager').hidden = result.TotalRecordCount <= 30; find('titlePrev').disabled = start === 0; find('titleNext').disabled = start + result.Items.length >= result.TotalRecordCount;
                text(find('titlePageInfo'), (start + 1) + '-' + (start + result.Items.length) + ' of ' + result.TotalRecordCount);
            }).catch(function (error) { if (token === state.titleToken) text(find('titleResults'), errorText(error)); });
        }
        function csvCell(value) {
            var str = value == null ? '' : String(value);
            if (/^[\s]*[=+@-]/.test(str) || /^[\t\r\n]/.test(str)) str = "'" + str;
            return '"' + str.replace(/"/g, '""') + '"';
        }
        function confirmEnabled() { find('confirmDelete').disabled = deletion.busy || deletion.state !== 'Ready' || !deletion.ready || !find('deleteAcknowledge').checked; }
        function previewFailed(message) {
            deletion.state = 'PreviewFailed'; deletion.busy = false; deletion.ready = 0; deletion.token = null;
            text(find('deletePhase'), 'Preview failed. No deletion was started for this selection.');
            text(find('deleteSummary'), message);
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('cancelDelete').hidden = true;
            find('deleteContinuation').hidden = true; find('deleteProgressArea').hidden = true;
            find('closeDelete').hidden = false; find('retryPreview').hidden = false; updateSelections();
        }
        function showDeletion(result) {
            if (!result || result.State === 'PreviewFailed' || !result.Token) {
                if (deletion.busy) { deletionStatusUnavailable('Emby did not return a valid operation status.'); return; }
                previewFailed(result && result.Message || 'Emby did not return a valid deletion preview.'); return;
            }
            ['Deleted','Ready','Skipped','Failed','Total','Processing','Synchronized','SyncFailed'].forEach(function (key) { if (typeof result[key] !== 'number') result[key] = 0; });
            deletion.token = result.Token; deletion.state = result.State; deletion.ready = result.Ready; deletion.busy = result.State === 'Deleting';
            text(find('deleteScope'), (result.RuleName ? 'Rule: ' + result.RuleName + '. ' : '') + (result.AllowUnwatched ? 'Preview scope: all watch states, including unfinished media and saved progress for any user. Shows expand into all available episodes.' : result.AllowWatchedResume ? 'Preview scope: fully watched media, including saved resume positions for completed viewers. Unfinished viewing for any user remains protected.' : 'Preview scope: fully watched media only. Saved partial playback and rewatches remain protected.'));
            var confirm = result.State === 'Ready' && result.Ready > 0;
            text(find('deletePhase'), result.State === 'Ready' ? (confirm ? 'Step 2 of 3: Review and confirm. No deletion has started.' : 'Nothing can be deleted. Review the blocked items below.') : result.State === 'Deleting' ? 'Step 3 of 3: Deleting confirmed media and updating configured services' : result.State === 'Cancelled' ? (result.Started === false ? 'Preview cancelled. No deletion started.' : 'Deletion cancelled or stopped') : (result.Failed || result.Skipped || result.SyncFailed ? 'Deletion finished with items to review' : 'Deletion completed'));
            find('retryPreview').hidden = true; find('deleteContinuation').hidden = result.State !== 'Deleting';
            var processed = result.Processed == null ? (result.Deleted || 0) + (result.Skipped || 0) + (result.Failed || 0) : result.Processed;
            find('deleteProgressArea').hidden = result.State === 'Ready' || result.Started === false || !result.Total;
            find('deleteProgress').max = Math.max(1, result.Total || 0); find('deleteProgress').value = processed;
            text(find('deleteProgressText'), processed + ' of ' + result.Total + ' processed' + (result.CurrentItemName ? '. Current item: ' + result.CurrentItemName : '') + '. ' + (result.Processing || 0) + ' currently processing.');
            text(find('deleteSummary'), result.Message + ' ' + result.Deleted + ' deleted; ' + result.Ready + ' ready; ' + result.Skipped + ' blocked or skipped; ' + result.Failed + ' media failures. ' + result.Synchronized + ' synchronized; ' + result.SyncFailed + ' synchronization issues.');
            find('deleteItems').replaceChildren();
            (result.Items || []).forEach(function (item) {
                var li = document.createElement('li'); text(li, item.Name + (item.Detail ? ' - ' + item.Detail : '') + ' (' + item.State + ')');
                var detail = document.createElement('small'); text(detail, (item.Paths || []).join('\n') + ((item.WatchedBy || []).length ? '\nWatched by: ' + item.WatchedBy.join(', ') : '') + (item.WatchNote ? '\n' + item.WatchNote : '') + (item.EmbyState ? '\nEmby: ' + item.EmbyState : '') + (item.ExternalService ? '\n' + item.ExternalService + ': ' + item.ExternalState + (item.ExternalPath ? '\nMatched service path: ' + item.ExternalPath : '') + (item.ExternalMessage ? '\n' + item.ExternalMessage : '') : '') + (item.Message ? '\n' + item.Message : '')); li.appendChild(detail); find('deleteItems').appendChild(li);
            });
            find('deleteConfirmFields').hidden = !confirm; find('confirmDelete').hidden = !confirm;
            text(find('confirmDelete'), 'Permanently delete ' + result.Ready + ' media item' + (result.Ready === 1 ? '' : 's'));
            find('cancelDelete').hidden = result.State !== 'Ready' && result.State !== 'Deleting';
            find('closeDelete').hidden = result.State === 'Ready'; text(find('closeDelete'), deletion.busy ? 'Close progress view' : 'Close and refresh');
            find('cancelDelete').disabled = false; text(find('cancelDelete'), deletion.busy ? 'Stop after current item' : (confirm ? 'Cancel' : 'Close preview'));
            confirmEnabled(); updateSelections();
            clearTimeout(deletionTimer);
            if (deletion.busy && state.active) deletionTimer = setTimeout(pollDeletion, 1000);
        }
        function previewDeletion(ids, fixedUserId, fixedAllowUnwatched) {
            if (deletion.busy || state.busy || retention.busy || !ids.length) return;
            if (ids.length > 500) { status('Select at most 500 titles per deletion batch.', true); return; }
            var request = ++deletion.request;
            deletion.reviewId = null; deletion.ids = ids.slice(); deletion.userId = arguments.length > 1 ? fixedUserId : (state.mode === 'user' ? find('userSelect').value : null);
            deletion.allowUnwatched = arguments.length > 2 ? fixedAllowUnwatched : find('allowUnwatched').checked;
            deletion.token = null; deletion.state = 'Loading'; deletion.ready = 0;
            text(find('deletePhase'), 'Step 1 of 3: Preparing preview for ' + ids.length + ' selected title' + (ids.length === 1 ? '' : 's') + '. No deletion has started.');
            find('deleteContinuation').hidden = true; find('deleteProgressArea').hidden = true; find('retryPreview').hidden = true;
            find('deleteAcknowledge').checked = false; find('deleteItems').replaceChildren();
            text(find('deleteScope'), deletion.allowUnwatched ? 'This preview allows partially watched and unwatched media, including saved progress for any user.' : 'This preview includes fully watched media only.');
            text(find('deleteSummary'), 'Checking selected media, Emby permissions, playback sessions, and deletion paths...');
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('cancelDelete').hidden = false;
            find('closeDelete').hidden = true; find('cancelDelete').disabled = false; text(find('cancelDelete'), 'Cancel');
            if (!find('deleteDialog').open) find('deleteDialog').showModal();
            deletionApi('Preview', { UserId: deletion.userId, ItemIds: ids.join(','), AllowUnwatched: deletion.allowUnwatched }).then(function (result) {
                if (request !== deletion.request || !state.active) { if (result && result.Token) deletionApi('Cancel', { Token: result.Token }).catch(function () {}); return; }
                showDeletion(result);
            }).catch(function (error) { if (request === deletion.request) previewFailed(deletionError(error)); });
        }
        function pollDeletion() {
            if (!state.active || !deletion.token || deletion.submitting) return;
            deletionApi('Status', { Token: deletion.token }, 'GET').then(showDeletion).catch(function (error) {
                deletionStatusUnavailable(deletionError(error));
            });
        }
        function deletionStatusUnavailable(message) {
            text(find('deletePhase'), 'Deletion status is unavailable. The confirmed operation may still be running.');
            text(find('deleteSummary'), message + ' Do not submit another deletion. Status will be checked again.');
            find('confirmDelete').hidden = true; find('deleteConfirmFields').hidden = true; find('retryPreview').hidden = true;
            find('cancelDelete').disabled = false; text(find('cancelDelete'), 'Stop remaining items');
            find('closeDelete').hidden = false; text(find('closeDelete'), 'Close progress view');
            updateSelections();
            if (state.active && deletion.token) { clearTimeout(deletionTimer); deletionTimer = setTimeout(pollDeletion, 3000); }
        }
        function executeDeletion() {
            if (find('confirmDelete').disabled || !find('deleteAcknowledge').checked) return;
            deletion.busy = true; deletion.submitting = true; deletion.state = 'Submitting'; confirmEnabled(); find('cancelDelete').disabled = true; updateSelections();
            text(find('deletePhase'), 'Step 3 of 3: Submitting confirmed deletion. Waiting for Emby...');
            find('deleteConfirmFields').hidden = true; find('confirmDelete').hidden = true; find('deleteContinuation').hidden = false;
            find('deleteProgressArea').hidden = false;
            find('closeDelete').hidden = false; text(find('closeDelete'), 'Close progress view');
            deletionApi('Execute', { Token: deletion.token, Confirmed: find('deleteAcknowledge').checked }).then(function (result) { deletion.submitting = false; showDeletion(result); }).catch(function (error) {
                deletion.submitting = false;
                deletionStatusUnavailable(deletionError(error));
                // A lost response must never trigger another destructive request.
                pollDeletion();
            });
        }
        function cancelDeletion() {
            ++deletion.request;
            if (!deletion.token) { find('deleteDialog').close(); return; }
            var wasRunning = deletion.busy;
            find('cancelDelete').disabled = true;
            deletionApi('Cancel', { Token: deletion.token }).then(function (result) { showDeletion(result); if (!wasRunning) find('deleteDialog').close(); }).catch(function (error) { text(find('deleteSummary'), deletionError(error)); find('cancelDelete').disabled = false; });
        }
        function exportCsv() {
            if (state.exporting || !state.total || state.busy) return;
            var path = state.mode === 'user' ? 'UserReport' : 'Viewers', parameters = reportParams(0, 500), all = [];
            state.exporting = true; find('export').disabled = true;
            function next() {
                status('Preparing CSV: ' + all.length + ' rows...');
                return api(path, parameters).then(function (result) {
                    if (!result.Items.length && parameters.StartIndex < result.TotalRecordCount) throw new Error('Report changed during export. Refresh and retry.');
                    all = all.concat(result.Items); parameters.StartIndex += result.Items.length;
                    if (parameters.StartIndex < result.TotalRecordCount) return next();
                    var header = ['User', 'Title', 'Type', 'Year', 'Series', 'Episode', 'Status', 'Watched count', 'In-progress count', 'Available episodes', 'Saved play count', 'Last played UTC', 'Not-started episodes', 'Watched-with-resume count', 'Episode completion percent', 'Saved position', 'Duration', 'Remaining time', 'Playback percent'];
                    var lines = [header.map(csvCell).join(',')];
                    all.forEach(function (row) { lines.push([row.UserName,row.Name,row.Type,row.Year,row.SeriesName,row.Episode,row.Status,row.WatchedCount,row.InProgressCount,row.AvailableEpisodeCount,row.SavedPlayCount,row.LastPlayedUtc,row.NotStartedCount,row.WatchedWithResumeCount,row.CompletionPercent,durationLabel(row.PlaybackPositionTicks),durationLabel(row.RunTimeTicks),durationLabel(row.RemainingTicks),row.PlaybackPercent].map(csvCell).join(',')); });
                    var url = URL.createObjectURL(new Blob(['\uFEFF' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }));
                    var link = document.createElement('a'); link.href = url; link.download = 'WhoSceneIt-' + new Date().toISOString().slice(0,10) + '.csv'; document.body.appendChild(link); link.click(); link.remove(); setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
                    status('Exported ' + all.length + ' rows.');
                });
            }
            next().catch(function (error) { status(errorText(error), true); }).then(function () { state.exporting = false; find('export').disabled = !state.total; });
        }
        function renderUpdate(update) {
            if (!state.active) return;
            if (update.AwaitingDashboardVerification && state.reportHealthy && document.visibilityState !== 'hidden') {
                api('Updates/Healthy', {}, 'POST').catch(function () {});
            }
            text(find('updateHeading'), 'Who Scene It ' + update.InstalledVersion + (update.UpdateAvailable ? ' | Available: ' + update.LatestVersion : ''));
            text(find('updateMessage'), update.Message);
            var busy = update.Busy;
            find('checkUpdate').disabled = !!busy;
            find('downloadUpdate').hidden = !update.UpdateAvailable || update.Downloaded || update.RestartRequired;
            find('downloadUpdate').disabled = !!busy || deletion.busy;
            find('installUpdate').hidden = !update.Downloaded || update.RestartRequired;
            find('installUpdate').disabled = !!busy || deletion.busy;
            find('restartServer').hidden = !update.RestartRequired || !update.CanRestart;
            find('restartServer').disabled = !!busy || deletion.busy;
            text(find('installUpdate'), update.CanRestart ? 'Install and restart' : 'Install update');
            find('restartWarning').hidden = !update.Downloaded || update.RestartRequired;
            text(find('restartWarning'), update.CanRestart ? 'Save open work. Installing this update restarts Emby Server and briefly disconnects its clients.' : 'Save open work. After installation, restart Emby Server using its normal controls.');
            find('releaseNotes').replaceChildren();
            (update.Notes || []).slice(0,3).forEach(function (note) { var li = document.createElement('li'); text(li, note); find('releaseNotes').appendChild(li); });
            find('releaseNotes').hidden = !update.UpdateAvailable || !(update.Notes || []).length;
            find('releaseLink').hidden = !update.UpdateAvailable || !update.HasMoreNotes;
            if (update.ReleaseUrl) find('releaseLink').href = update.ReleaseUrl;
            clearTimeout(pollTimer);
            pollTimer = setTimeout(pollUpdate, busy || update.RestartRequired ? 2000 : 30000);
        }
        function pollUpdate() {
            if (!state.active) return;
            api('Updates').then(renderUpdate).catch(function () { if (state.active) { text(find('updateMessage'), 'Emby Server is unreachable. Waiting to reconnect...'); pollTimer = setTimeout(pollUpdate, 3000); } });
        }
        function updateAction(action, reason) {
            if (deletion.busy && (action === 'Install' || action === 'Restart' || action === 'Download')) return;
            find('checkUpdate').disabled = true; find('downloadUpdate').disabled = true; find('installUpdate').disabled = true;
            api('Updates/' + action, { Reason: reason || 'manual' }, 'POST').then(renderUpdate).catch(function (error) { text(find('updateMessage'), errorText(error)); pollTimer = setTimeout(pollUpdate, 2000); });
        }
        function focus() { if (state.active && document.visibilityState !== 'hidden') updateAction('Check', 'focus'); }
        function init() {
            if (state.initialized) { loadReport(); pollUpdate(); loadRetention(); retentionApi('Dashboard',{}).catch(function () {}); return; }
            state.initialized = true;
            find('userTab').addEventListener('click', function () { changeMode('user'); });
            find('titleTab').addEventListener('click', function () { changeMode('title'); });
            ['userSelect','viewSelect','sort','watchState'].forEach(function (id) { find(id).addEventListener('change', function () { if (id === 'viewSelect' && find(id).value !== 'Episodes') { state.series = null; find('episodeScope').hidden = true; } state.start = 0; loadReport(); }); });
            find('clearSeries').addEventListener('click', function () { state.series = null; find('episodeScope').hidden = true; find('viewSelect').value = 'Shows'; state.start = 0; loadReport(); });
            find('filter').addEventListener('input', function () { clearTimeout(filterTimer); filterTimer = setTimeout(function () { state.start = 0; loadReport(); }, 300); });
            find('titleSearchForm').addEventListener('submit', function (event) { event.preventDefault(); searchTitles(0); });
            find('titlePrev').addEventListener('click', function () { searchTitles(Math.max(0,state.titleStart-30)); });
            find('titleNext').addEventListener('click', function () { searchTitles(state.titleStart+30); });
            find('refresh').addEventListener('click', function () { loadReport(); });
            find('prev').addEventListener('click', function () { state.start = Math.max(0,state.start-pageSize); loadReport(); });
            find('next').addEventListener('click', function () { state.start += pageSize; loadReport(); });
            find('export').addEventListener('click', exportCsv);
            find('saveIntegrations').addEventListener('click', saveIntegrations);
            find('reloadIntegrations').addEventListener('click', loadIntegrations);
            find('testSonarr').addEventListener('click', function () { testIntegration('Sonarr'); });
            find('testRadarr').addEventListener('click', function () { testIntegration('Radarr'); });
            find('clearSelection').addEventListener('click', resetSelections);
            find('protectSelected').addEventListener('click', function () { protectMedia(Array.from(selections),true); });
            find('protectTitle').addEventListener('click', function () { if (!state.selected) return; var flag = retention.flags[state.selected.Id] || {}; protectMedia([flag.SourceId || state.selected.Id], !flag.NeverDelete); });
            find('cleanupForm').addEventListener('submit', saveCleanup);
            find('cleanupForm').addEventListener('change', function (event) { if (event.target !== find('cleanupAuthorize')) find('cleanupAuthorize').checked = false; cleanupControls(); });
            find('cleanupForm').addEventListener('input', function (event) { if (event.target !== find('cleanupAuthorize')) find('cleanupAuthorize').checked = false; cleanupControls(); });
            find('cleanupRule').addEventListener('change', function () { editCleanup(find('cleanupRule').value); });
            find('newCleanup').addEventListener('click', function () { editCleanup(null); });
            find('evaluateCleanup').addEventListener('click', function () { retentionAction('Evaluate',{Id:retention.editing}); });
            find('removeCleanup').addEventListener('click', function () { retentionAction('Policy/Remove',{Id:retention.editing}).then(function () { editCleanup(null); }); });
            find('reloadCleanup').addEventListener('click', loadRetention);
            find('cleanupHoldList').addEventListener('change', function () { find('cleanupHoldAuthorize').checked = false; cleanupControls(); });
            find('cleanupHoldAuthorize').addEventListener('change', cleanupControls);
            find('clearCleanupHold').addEventListener('click', function () { if (find('clearCleanupHold').disabled) return; var value = find('cleanupHoldList').value.split('/'); retentionAction('Hold/Clear',{RunId:value[0],ItemId:value[1],Confirmed:find('cleanupHoldAuthorize').checked}); find('cleanupHoldAuthorize').checked = false; });
            find('protectedList').addEventListener('change', cleanupControls);
            find('removeProtection').addEventListener('click', function () { if (find('protectedList').value) protectMedia([find('protectedList').value], false); });
            find('deleteSelected').addEventListener('click', function () { previewDeletion(Array.from(selections)); });
            find('deleteTitle').addEventListener('click', function () { if (state.selected) previewDeletion([state.selected.Id]); });
            find('allowUnwatched').addEventListener('change', function () { selections.clear(); state.start = 0; if (find('allowUnwatched').checked && find('watchState').value === 'Started') find('watchState').value = 'All'; loadReport(); });
            find('deleteAcknowledge').addEventListener('change', confirmEnabled);
            find('confirmDelete').addEventListener('click', executeDeletion); find('cancelDelete').addEventListener('click', cancelDeletion);
            find('retryPreview').addEventListener('click', function () { if (deletion.reviewId) openCleanupReview(deletion.reviewId); else previewDeletion(deletion.ids, deletion.userId, deletion.allowUnwatched); });
            find('viewDeletion').addEventListener('click', function () { if (!find('deleteDialog').open) find('deleteDialog').showModal(); pollDeletion(); });
            find('closeDelete').addEventListener('click', function () { find('deleteDialog').close(); if (!deletion.busy) { resetSelections(); loadReport(); pollUpdate(); } });
            find('deleteDialog').addEventListener('cancel', function (event) { event.preventDefault(); if (!deletion.busy) cancelDeletion(); });
            find('checkUpdate').addEventListener('click', function () { updateAction('Check','manual'); });
            find('downloadUpdate').addEventListener('click', function () { updateAction('Download'); });
            find('installUpdate').addEventListener('click', function () { updateAction('Install'); });
            find('restartServer').addEventListener('click', function () { updateAction('Restart'); });
            api('Users').then(function (users) {
                find('reportApp').hidden = false; find('adminError').hidden = true;
                loadIntegrations();
                users.forEach(function (user) { var option = document.createElement('option'); option.value = user.Id; text(option,user.Name + (user.IsDisabled ? ' (disabled)' : '')); find('userSelect').appendChild(option); find('cleanupUser').appendChild(option.cloneNode(true)); });
                editCleanup(null); loadRetention(); retentionApi('Dashboard',{}).catch(function () {}); loadReport();
            }).catch(function () { find('adminError').hidden = false; find('reportApp').hidden = true; });
            pollUpdate();
        }
        view.addEventListener('viewshow', function () { state.active = true; init(); window.addEventListener('focus',focus); document.addEventListener('visibilitychange',focus); focus(); if (deletion.busy) { if (!find('deleteDialog').open) find('deleteDialog').showModal(); pollDeletion(); } });
        view.addEventListener('viewhide', function () {
            state.active = false; state.token++; state.titleToken++; deletion.request++;
            clearTimeout(pollTimer); clearTimeout(filterTimer); clearTimeout(deletionTimer); clearTimeout(retentionTimer);
            window.removeEventListener('focus',focus); document.removeEventListener('visibilitychange',focus);
            if (find('deleteDialog').open) find('deleteDialog').close();
            if (deletion.token && !deletion.busy && deletion.state === 'Ready') { deletionApi('Cancel', { Token: deletion.token }).catch(function () {}); deletion.state = 'Cancelled'; }
        });
    };
});

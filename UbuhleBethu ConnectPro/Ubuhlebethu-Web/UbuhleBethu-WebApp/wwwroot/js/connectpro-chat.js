/*
 * ConnectPro assistant client (shared by the floating widget and the full-page assistant).
 *  - keeps the whole conversation (sessionStorage) so the model has real context on every turn
 *  - renders model markdown safely (everything is HTML-escaped first; no raw HTML ever reaches the DOM)
 *  - never labels normal answers as "offline"; only a genuine provider outage shows a small notice
 */
(function (global) {
    'use strict';

    var MAX_HISTORY = 16;

    function esc(s) {
        return String(s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function inline(t) {
        t = esc(t);
        t = t.replace(/`([^`]+)`/g, '<code>$1</code>');
        t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
        t = t.replace(/(^|[\s(])\*([^*\s][^*]*)\*(?=[\s).,!?:;]|$)/g, '$1<em>$2</em>');
        // [text](url) - only http(s) or same-site relative links
        t = t.replace(/\[([^\]]+)\]\(((?:https?:\/\/|\/)[^)\s]+)\)/g, function (m, label, url) {
            return '<a href="' + url + '" target="_blank" rel="noopener noreferrer">' + label + '</a>';
        });
        return t;
    }

    /** Minimal, safe markdown: headings, bold/italic/code, bullet + numbered lists, tables, paragraphs. */
    function renderMarkdown(src) {
        var lines = String(src || '').replace(/\r\n/g, '\n').split('\n');
        var out = [], list = null, table = null;

        function closeList() { if (list) { out.push('</' + list + '>'); list = null; } }
        function closeTable() {
            if (!table) return;
            var html = '<table class="table table-sm table-bordered mb-2" style="font-size:.85em"><thead><tr>';
            table.rows[0].forEach(function (c) { html += '<th>' + inline(c) + '</th>'; });
            html += '</tr></thead><tbody>';
            table.rows.slice(1).forEach(function (r) {
                html += '<tr>'; r.forEach(function (c) { html += '<td>' + inline(c) + '</td>'; }); html += '</tr>';
            });
            out.push(html + '</tbody></table>');
            table = null;
        }

        lines.forEach(function (raw) {
            var line = raw.trimEnd(), m;

            if (/^\s*\|.*\|\s*$/.test(line)) {
                closeList();
                var cells = line.trim().replace(/^\||\|$/g, '').split('|').map(function (c) { return c.trim(); });
                if (cells.every(function (c) { return /^:?-{2,}:?$/.test(c); })) return; // separator row
                if (!table) table = { rows: [] };
                table.rows.push(cells);
                return;
            }
            closeTable();

            if (!line.trim()) { closeList(); return; }
            if ((m = /^\s{0,3}#{1,6}\s+(.*)$/.exec(line))) { closeList(); out.push('<div class="fw-bold mt-2 mb-1">' + inline(m[1]) + '</div>'); return; }
            if ((m = /^\s*[-*•]\s+(.*)$/.exec(line))) {
                if (list !== 'ul') { closeList(); out.push('<ul class="ps-3 mb-1">'); list = 'ul'; }
                out.push('<li>' + inline(m[1]) + '</li>'); return;
            }
            if ((m = /^\s*(\d+)[.)]\s+(.*)$/.exec(line))) {
                if (list !== 'ol') { closeList(); out.push('<ol class="ps-3 mb-1">'); list = 'ol'; }
                out.push('<li>' + inline(m[2]) + '</li>'); return;
            }
            closeList();
            out.push('<p class="mb-1">' + inline(line) + '</p>');
        });
        closeList(); closeTable();
        return out.join('');
    }

    function Conversation(storageKey) {
        var key = 'connectpro_chat_' + (storageKey || 'default');
        var history = [];
        try { history = JSON.parse(sessionStorage.getItem(key) || '[]'); } catch (e) { history = []; }
        if (!Array.isArray(history)) history = [];

        function save() {
            try { sessionStorage.setItem(key, JSON.stringify(history.slice(-MAX_HISTORY * 2))); } catch (e) { /* storage full/blocked */ }
        }

        return {
            messages: function () { return history.slice(); },
            clear: function () { history = []; save(); },
            /** Sends the question with prior turns; resolves { answer, fallback, source }. */
            send: function (question, provider) {
                var prior = history.slice(-MAX_HISTORY);
                history.push({ role: 'user', content: question }); save();
                return fetch('/api/chatbot/ask', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ question: question, provider: provider || 'auto', history: prior })
                }).then(function (r) {
                    return r.json().catch(function () { return {}; }).then(function (b) {
                        if (!r.ok) throw new Error((b && (b.error || b.answer || b.Answer)) || ('HTTP ' + r.status));
                        var answer = b.answer || b.Answer || 'I could not produce an answer. Please try rephrasing.';
                        history.push({ role: 'assistant', content: answer }); save();
                        return { answer: answer, fallback: !!(b.fallback || b.Fallback), source: b.source || b.Source || '' };
                    });
                }).catch(function (err) {
                    history.pop(); save(); // do not keep the unanswered question in context
                    throw err;
                });
            }
        };
    }

    global.ConnectProChat = { renderMarkdown: renderMarkdown, escape: esc, Conversation: Conversation };
})(window);

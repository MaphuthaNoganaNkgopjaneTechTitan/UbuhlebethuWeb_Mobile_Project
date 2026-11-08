function exportTableToPdf(tableId, title, fileName) {
    var table = document.getElementById(tableId);
    if (!table) { alert('Nothing to export.'); return; }
    if (!window.jspdf) { alert('The PDF library did not load. Check your internet connection.'); return; }

    var keep = [], head = [];
    table.querySelectorAll('thead th').forEach(function(th, i) {
        var name = th.textContent.trim();
        if (th.hasAttribute('data-no-export') || name.toLowerCase() === 'actions') return;
        keep.push(i); head.push(name);
    });

    function cellText(td) {
        var clone = td.cloneNode(true);
        clone.querySelectorAll('[data-no-export]').forEach(function(n) { n.remove(); });
        return clone.textContent.replace(/\s+/g, ' ').trim();
    }

    var body = [];
    table.querySelectorAll('tbody tr').forEach(function(tr) {
        if (tr.style.display === 'none') return;
        var cells = tr.querySelectorAll('td');
        body.push(keep.map(function(i) { return cells[i] ? cellText(cells[i]) : ''; }));
    });

    var doc = new window.jspdf.jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });
    doc.setFontSize(16);
    doc.text(title, 40, 40);
    doc.setFontSize(10);
    doc.text('Generated ' + new Date().toLocaleString(), 40, 58);
    doc.autoTable({ head: [head], body: body, startY: 72, styles: { fontSize: 10 }, headStyles: { fillColor: [155, 28, 49] } });
    doc.save((fileName || 'export') + '.pdf');
}
# Eski Yazırma ve Excel Kodları (Backup)

## Excel Butonu (btnExcel) - Satırlar 638-673

```javascript
// Excel indir
document.getElementById('btnExcel').addEventListener('click', function() {
    var ay = parseInt(document.getElementById('monthSelect').value);
    var yil = document.getElementById('yearSelect').value;
    var birim = document.getElementById('birimSelect').value;
    var tbl = document.getElementById('mesaiTable').cloneNode(true);
    // no-print sütunları kaldır
    tbl.querySelectorAll('.no-print').forEach(function(el) { el.remove(); });
    // input'ları değerlere çevir
    tbl.querySelectorAll('input, select').forEach(function(el) {
        var td = el.parentElement;
        td.textContent = el.value;
    });

    var html = '<html><head><meta charset="UTF-8"><style>th,td{border:1px solid #000;padding:5px;text-align:center;}th{background:#1e3c72;color:white;font-weight:bold;}h2{margin:0;color:#1e3c72;}h3{margin:5px 0;color:#333;}p{margin:0;color:#666;font-size:12px;}</style></head><body>' +
        '<div style="text-align:center;margin-bottom:15px;">' +
        '<h2>GAMABEL YATIRIM LTD</h2>' +
        '<h3>EK MESAİ ÇİZELGESİ</h3>' +
        '<h2>' + escapeHtml(birim) + ' - ' + ayAdlari[ay].toUpperCase() + ' ' + yil + '</h2>' +
        '</div>' +
        tbl.outerHTML +
        '<br><table style="width:100%;margin-top:40px;"><tr>' +
        '<td style="width:33%;text-align:center;border:none;">Birim Sorumlusu<br>_________________</td>' +
        '<td style="width:33%;text-align:center;border:none;">Başkan / Onay<br>_________________</td>' +
        '</tr></table></body></html>';

    var blob = new Blob([html], { type: 'application/vnd.ms-excel;charset=UTF-8' });
    var link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = 'Ek_Mesai_' + ayAdlari[ay] + '_' + yil + '.xls';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(link.href);
    showToast('Excel indiriliyor');
});
```

---

## Yazdır Butonu (btnYazdir) - Satırlar 882-900

```javascript
// Yazdır
document.getElementById('btnYazdir').addEventListener('click', function() {
    var layout = renderMesaiPrintLayout();
    var printDiv = layout.div;

    // Geçici container'a ekle
    var container = document.createElement('div');
    container.id = 'printContainer';
    container.appendChild(printDiv);
    document.body.appendChild(container);

    // Yazdır dialog'unu aç
    setTimeout(function() {
        window.print();
    }, 300);

    // Yazdırma bitince container'ı sil
    window.addEventListener('afterprint', function() {
        container.remove();
    }, { once: true });
});
```

---

## HTML Buton Tanımları (Satır 136, 139)

```html
<button class="btn btn-sm btn-info text-white" id="btnExcel" disabled>Excel</button>
<!-- ... -->
<button class="btn btn-sm btn-secondary" id="btnYazdir">Yazdır</button>
```

---

## Notlar
- Bu kodlar Ek Mesai Çizelgesi (Views/PRS/Mesai/Index.cshtml) sayfasından çıkarılmıştır
- PDF butonları (btnBirimPdf, btnPersonelPdf) aktif tutulmuştur
- Yazdır ve Excel fonksiyonları renderMesaiPrintLayout() ve renderPersonelMesaiLayout() kullanmaktadır

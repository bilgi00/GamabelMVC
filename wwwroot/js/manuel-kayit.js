document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('manuelTalimatForm');
    const rows = document.getElementById('invoiceRows');
    const template = document.getElementById('invoiceRowTemplate');
    const addRowButton = document.getElementById('addRowBtn');
    const saveDraftButton = document.getElementById('saveDraftBtn');
    const maximumRows = 200;
    const maximumAmount = 999999999.99;
    let dirty = false;

    function normalize(value) {
        return value.trim().toLocaleLowerCase('tr-TR');
    }

    function isRowPopulated(row) {
        return Boolean(
            row.querySelector('.firm-select').value ||
            row.querySelector('input[name$=".FaturaAdi"]').value.trim() ||
            row.querySelector('.amount-input').value
        );
    }

    function updateSummary() {
        let totalCents = 0;
        let completeCount = 0;
        let incompleteCount = 0;
        const subtotals = new Map();
        const duplicates = new Map();

        rows.querySelectorAll('.invoice-row').forEach(function (row) {
            const firmSelect = row.querySelector('.firm-select');
            const invoiceName = row.querySelector('input[name$=".FaturaAdi"]').value.trim();
            const amount = Number(row.querySelector('.amount-input').value.replace(',', '.'));
            const populated = isRowPopulated(row);
            const complete = Boolean(firmSelect.value && invoiceName && amount > 0 && Number.isFinite(amount));

            if (populated && !complete) {
                incompleteCount++;
            }

            if (!complete) {
                return;
            }

            const amountCents = Math.round(amount * 100);
            const companyName = firmSelect.selectedOptions[0].textContent.trim();
            totalCents += amountCents;
            completeCount++;
            subtotals.set(firmSelect.value, {
                name: companyName,
                cents: (subtotals.get(firmSelect.value)?.cents || 0) + amountCents
            });

            const duplicateKey = firmSelect.value + '|' + normalize(invoiceName);
            duplicates.set(duplicateKey, (duplicates.get(duplicateKey) || 0) + 1);
        });

        let duplicateCount = 0;
        rows.querySelectorAll('.invoice-row').forEach(function (row) {
            const firmId = row.querySelector('.firm-select').value;
            const invoiceName = row.querySelector('input[name$=".FaturaAdi"]').value.trim();
            const duplicateKey = firmId + '|' + normalize(invoiceName);
            const duplicate = Boolean(firmId && invoiceName && duplicates.get(duplicateKey) > 1);
            row.classList.toggle('is-duplicate', duplicate);
            if (duplicate) {
                duplicateCount++;
            }
        });

        document.getElementById('toplamTutar').textContent =
            'Toplam: ' + (totalCents / 100).toLocaleString('tr-TR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }) + ' TL';

        let countText = completeCount + ' fatura';
        if (incompleteCount) {
            countText += ' (' + incompleteCount + ' satır eksik)';
        }
        document.getElementById('faturaSayisi').textContent = countText;

        const duplicateWarning = document.getElementById('duplicateWarning');
        duplicateWarning.hidden = duplicateCount === 0;
        duplicateWarning.textContent = duplicateCount
            ? 'Aynı firma ve fatura adı birden fazla satırda bulunuyor. Talimat oluşturmadan önce vurgulanan satırları kontrol edin.'
            : '';

        return { totalCents: totalCents, subtotals: subtotals, completeCount: completeCount, incompleteCount: incompleteCount, duplicateCount: duplicateCount };
    }

    function reindexRows() {
        rows.querySelectorAll('.invoice-row').forEach(function (row, index) {
            row.querySelectorAll('[name]').forEach(function (input) {
                input.name = input.name.replace(/Satirlar\[\d+\]/, 'Satirlar[' + index + ']');
            });
            row.querySelectorAll('[data-valmsg-for]').forEach(function (message) {
                message.dataset.valmsgFor = message.dataset.valmsgFor.replace(/Satirlar\[\d+\]/, 'Satirlar[' + index + ']');
            });
        });
    }

    function copyCompanyOptions(row) {
        const target = row.querySelector('.firm-select');
        if (target.options.length > 1) {
            return;
        }

        const source = rows.querySelector('.firm-select');
        Array.from(source.options).slice(1).forEach(function (option) {
            const copy = option.cloneNode(true);
            copy.selected = false;
            copy.removeAttribute('selected');
            copy.hidden = false;
            target.add(copy);
        });
    }

    function filterCompanyOptions(searchInput) {
        const select = searchInput.closest('.invoice-row').querySelector('.firm-select');
        const query = normalize(searchInput.value);
        Array.from(select.options).forEach(function (option, index) {
            option.hidden = index > 0 && option.value !== select.value && !normalize(option.textContent).includes(query);
        });
    }

    function addRow(focusSearch) {
        const count = rows.querySelectorAll('.invoice-row').length;
        if (count >= maximumRows) {
            document.getElementById('pasteFeedback').textContent = 'En fazla ' + maximumRows + ' fatura satırı ekleyebilirsiniz.';
            return null;
        }

        const fragment = template.content.cloneNode(true);
        const row = fragment.querySelector('.invoice-row');
        row.innerHTML = row.innerHTML.replaceAll('__index__', String(count));
        rows.appendChild(fragment);
        const addedRow = rows.lastElementChild;
        copyCompanyOptions(addedRow);

        if (window.jQuery && window.jQuery.validator && window.jQuery.validator.unobtrusive) {
            window.jQuery(form).removeData('validator').removeData('unobtrusiveValidation');
            window.jQuery.validator.unobtrusive.parse(form);
        }

        updateSummary();
        if (focusSearch) {
            addedRow.querySelector('.firm-search').focus();
        }
        dirty = true;
        return addedRow;
    }

    function addRowButtonHandler() {
        addRow(true);
    }

    addRowButton.addEventListener('click', addRowButtonHandler);

    saveDraftButton.addEventListener('click', function () {
        const action = document.createElement('input');
        action.type = 'hidden';
        action.name = 'islem';
        action.value = 'taslak';
        form.appendChild(action);
        dirty = false;
        saveDraftButton.disabled = true;
        saveDraftButton.textContent = 'Taslak kaydediliyor...';
        HTMLFormElement.prototype.submit.call(form);
    });

    rows.addEventListener('click', function (event) {
        const button = event.target.closest('.remove-row');
        if (!button) {
            return;
        }

        const row = button.closest('.invoice-row');
        if (rows.querySelectorAll('.invoice-row').length === 1) {
            row.querySelector('.firm-select').value = '';
            row.querySelector('.firm-search').value = '';
            row.querySelector('input[name$=".FaturaAdi"]').value = '';
            row.querySelector('.amount-input').value = '';
        } else {
            row.remove();
            reindexRows();
        }

        dirty = true;
        updateSummary();
    });

    rows.addEventListener('input', function (event) {
        if (event.target.matches('.firm-search')) {
            filterCompanyOptions(event.target);
        }
        dirty = true;
        updateSummary();
    });

    rows.addEventListener('change', function (event) {
        if (event.target.matches('.firm-select')) {
            event.target.closest('.invoice-row').querySelector('.firm-search').value = '';
            filterCompanyOptions(event.target.closest('.invoice-row').querySelector('.firm-search'));
        }
        dirty = true;
        updateSummary();
    });

    rows.addEventListener('keydown', function (event) {
        if (event.key === 'Enter' && event.target.matches('.amount-input')) {
            event.preventDefault();
            addRow(true);
        }

        if (event.key === 'Enter' && event.target.matches('.firm-search')) {
            event.preventDefault();
            event.target.closest('.invoice-row').querySelector('.firm-select').focus();
        }
    });

    function normalizePastedAmount(value) {
        let normalized = value.trim().replace(/\s/g, '');
        if (normalized.includes(',') && normalized.includes('.')) {
            normalized = normalized.replace(/\./g, '').replace(',', '.');
        } else {
            normalized = normalized.replace(',', '.');
        }
        const amount = Number(normalized);
        return Number.isFinite(amount) && amount >= 0.01 && amount <= maximumAmount
            ? amount.toFixed(2)
            : '';
    }

    rows.addEventListener('paste', function (event) {
        const pastedText = event.clipboardData?.getData('text');
        if (!pastedText || !pastedText.includes('\t')) {
            return;
        }

        event.preventDefault();
        const lines = pastedText.trim().split(/\r?\n/).filter(Boolean);
        const companyOptions = Array.from(rows.querySelector('.firm-select').options).slice(1);
        const blankRow = rows.querySelector('.invoice-row');
        const firstRowBlank = !isRowPopulated(blankRow);
        let currentRow = firstRowBlank ? blankRow : null;
        let importedCount = 0;
        const errors = [];

        lines.forEach(function (line, lineIndex) {
            const cells = line.split('\t');
            if (cells.length < 3) {
                errors.push((lineIndex + 1) + '. satırda firma, fatura ve tutar sütunları bulunamadı.');
                return;
            }

            const companyValue = normalize(cells[0]);
            const company = companyOptions.find(function (option) {
                const labelParts = option.textContent.split('—').map(normalize);
                return option.value === cells[0].trim() ||
                    labelParts.includes(companyValue) ||
                    normalize(option.textContent) === companyValue;
            });
            const amount = normalizePastedAmount(cells[2]);
            if (!company || !cells[1].trim() || !amount) {
                errors.push((lineIndex + 1) + '. satır aktarılmadı; firma, fatura adı ve 0,01–999.999.999,99 TL tutarını kontrol edin.');
                return;
            }

            if (!currentRow) {
                currentRow = addRow(false);
            }
            if (!currentRow) {
                errors.push('Satır sınırına ulaşıldığı için kalan satırlar aktarılmadı.');
                return;
            }

            currentRow.querySelector('.firm-select').value = company.value;
            currentRow.querySelector('.firm-search').value = '';
            currentRow.querySelector('input[name$=".FaturaAdi"]').value = cells[1].trim();
            currentRow.querySelector('.amount-input').value = amount;
            importedCount++;
            currentRow = null;
        });

        document.getElementById('pasteFeedback').textContent = [
            importedCount ? importedCount + ' satır Excel’den aktarıldı.' : '',
            ...errors
        ].filter(Boolean).join(' ');
        dirty = dirty || importedCount > 0;
        updateSummary();
    });

    form.addEventListener('input', function () {
        dirty = true;
    });

    form.addEventListener('change', function () {
        dirty = true;
    });

    form.addEventListener('submit', function (event) {
        const submitter = event.submitter;
        if (!form.checkValidity()) {
            return;
        }

        const summary = updateSummary();
        const bank = form.querySelector('[name="BankaId"]');
        const bankName = bank.selectedOptions[0]?.textContent.trim() || 'Seçilmedi';
        const date = form.querySelector('[name="TalimatTarihi"]').value;
        const companyLines = Array.from(summary.subtotals.values()).map(function (item) {
            return '• ' + item.name + ': ' + (item.cents / 100).toLocaleString('tr-TR', {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2
            }) + ' TL';
        });
        const warnings = [];
        if (summary.incompleteCount) {
            warnings.push(summary.incompleteCount + ' eksik satır var.');
        }
        if (summary.duplicateCount) {
            warnings.push('Mükerrer fatura satırları var.');
        }

        const confirmation = [
            'Ödeme talimatı oluşturulup kaydedilecek. Bilgileri kontrol edin:',
            'Banka: ' + bankName,
            'Tarih: ' + date,
            ...companyLines,
            'Genel toplam (' + summary.completeCount + ' fatura): ' +
                (summary.totalCents / 100).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' TL',
            ...warnings.map(function (warning) { return 'Uyarı: ' + warning; })
        ].join('\n');

        if (!window.confirm(confirmation)) {
            event.preventDefault();
            return;
        }

        if (submitter) {
            submitter.disabled = true;
            submitter.textContent = 'Talimat oluşturuluyor...';
        }
        dirty = false;
    });

    window.addEventListener('beforeunload', function (event) {
        if (!dirty) {
            return;
        }
        event.preventDefault();
        event.returnValue = '';
    });

    updateSummary();
});

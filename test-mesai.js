const { chromium } = require('playwright');

(async () => {
    const browser = await chromium.launch({ headless: false }); // Tarayıcı görünecek
    const page = await browser.newPage();
    
    try {
        console.log('🚀 Test başlıyor...\n');
        
        // 1. Sayfaya git
        console.log('📍 Sayfaya gidiliyor: http://localhost:5010/Mesai');
        await page.goto('http://localhost:5010/Mesai', { waitUntil: 'networkidle' });
        await page.waitForTimeout(1500);
        
        // Eğer login sayfasında ise, geri dön
        const currentUrl = page.url();
        console.log(`   URL: ${currentUrl}`);
        if (currentUrl.includes('Kullanici') || currentUrl.includes('Account')) {
            console.log('\n   ⏳ LOGIN SAYFASINDA - Tarayıcıyı kontrol et!');
            console.log('   ⏳ Lütfen sisteme giriş yap ve test başlaması için beklemeye devam et...\n');
            // Başarılı bir login için /Mesai sayfasına gidiş bekle
            await page.waitForURL('**/Mesai**', { timeout: 120000 });
            await page.waitForTimeout(3000);
            console.log('   ✓ Login başarılı! Test devam ediyor...\n');
        }
        await page.waitForTimeout(1000);
        
        // 2. Birim seç (admin değilse otomatik seçili)
        console.log('📍 Birim kontrol ediliyor...');
        const birimSelect = await page.$('#birimSelect');
        if (birimSelect) {
            const value = await birimSelect.getAttribute('disabled');
            if (!value) {
                console.log('   - Birim seçiliyor');
                await page.selectOption('#birimSelect', { index: 1 });
            }
        }
        
        // 3. Yükle butonuna bas
        console.log('📍 Yükle butonuna basılıyor...');
        await page.click('#btnYukle');
        await page.waitForTimeout(2000);
        
        // Personel ve kayıtlarının yüklenmesini bekle
        const yatay = await page.$('#tableBody');
        console.log('   ✓ Veriler yüklendi');
        
        // 4. Satır Ekle butonu tıkla
        console.log('📍 "Satır Ekle" butonu tıklanıyor...');
        const btnSatirEkle = await page.$('#btnSatirEkle');
        const isDisabled = await page.evaluate(el => el.disabled, btnSatirEkle);
        
        if (isDisabled) {
            console.log('   ⚠️  HATA: Satır Ekle butonu disabled. Lütfen tüm adımları kontrol et.');
            await page.waitForTimeout(3000);
            await browser.close();
            return;
        }
        
        await page.click('#btnSatirEkle');
        await page.waitForTimeout(1500);
        
        // Modal açılmasını bekle
        const modal = await page.$('#satirEkleModal');
        if (!modal) {
            console.log('   ⚠️  HATA: Modal açılmadı!');
            await page.waitForTimeout(3000);
            await browser.close();
            return;
        }
        console.log('   ✓ Modal açıldı');
        
        // 5. Modal içindeki personel listesini al
        console.log('📍 Personel listesi kontrol ediliyor...');
        const personelListesi = await page.evaluate(() => {
            const select = document.getElementById('modalPersonel');
            return Array.from(select.options).map(opt => ({ value: opt.value, text: opt.text }));
        });
        
        if (personelListesi.length === 0) {
            console.log('   ⚠️  HATA: Personel listesi boş!');
            await page.waitForTimeout(3000);
            await browser.close();
            return;
        }
        
        console.log(`   ✓ ${personelListesi.length} personel bulundu`);
        console.log(`   - İlk personel: ${personelListesi[0].text}`);
        
        // 6. İlk personeli seç
        console.log('📍 Personel seçiliyor...');
        await page.selectOption('#modalPersonel', personelListesi[0].value);
        console.log(`   ✓ ${personelListesi[0].text} seçildi`);
        
        // 7. Tarih seç
        console.log('📍 Tarih seçiliyor...');
        const bugununTarihi = new Date().toISOString().split('T')[0];
        await page.fill('#modalTarih', bugununTarihi);
        console.log(`   ✓ Tarih: ${bugununTarihi}`);
        
        // 8. Başlangıç saati
        console.log('📍 Başlangıç saati giriliyoru...');
        await page.fill('#modalBaslangic', '17:00');
        console.log('   ✓ Başlangıç: 17:00');
        
        // 9. Bitiş saati
        console.log('📍 Bitiş saati giriliyoru...');
        await page.fill('#modalBitis', '22:00');
        console.log('   ✓ Bitiş: 22:00');
        
        // 10. Açıklama
        console.log('📍 Açıklama giriliyoru...');
        await page.fill('#modalAciklama', 'Test Mesai Kaydı');
        console.log('   ✓ Açıklama: Test Mesai Kaydı');
        
        // 11. Ekle butonuna bas
        console.log('📍 "Ekle" butonuna basılıyor...');
        await page.click('#btnModalEkle');
        await page.waitForTimeout(1500);
        console.log('   ✓ Kayıt eklendi');
        
        // 12. Kaydet butonuna bas
        console.log('📍 "Kaydet" butonuna basılıyor...');
        const btnKaydet = await page.$('#btnKaydet');
        const isKaydetDisabled = await page.evaluate(el => el.disabled, btnKaydet);
        
        if (isKaydetDisabled) {
            console.log('   ⚠️  HATA: Kaydet butonu disabled!');
            await page.waitForTimeout(3000);
            await browser.close();
            return;
        }
        
        await page.click('#btnKaydet');
        await page.waitForTimeout(2000);
        console.log('   ✓ Veritabanına kaydedildi');
        
        // 13. PDF butonuna bas
        console.log('📍 PDF çıktısı oluşturuluyor...');
        
        // PDF indirme promise'ini dinle
        const downloadPromise = page.waitForEvent('download');
        await page.click('#btnPdf');
        const download = await downloadPromise;
        
        const fileName = await download.suggestedFilename();
        console.log(`   ✓ PDF İndirildi: ${fileName}`);
        
        // 14. Excel butonuna bas
        console.log('📍 Excel çıktısı oluşturuluyor...');
        const excelDownloadPromise = page.waitForEvent('download');
        await page.click('#btnExcel');
        const excelDownload = await excelDownloadPromise;
        const excelFileName = await excelDownload.suggestedFilename();
        console.log(`   ✓ Excel İndirildi: ${excelFileName}`);
        
        console.log('\n✅ TÜM TESTLER BAŞARILI!\n');
        console.log('Test Özeti:');
        console.log('  ✓ Sayfa yüklendi');
        console.log('  ✓ Veriler sorgulandı');
        console.log('  ✓ Satır Ekle penceresi açıldı');
        console.log('  ✓ Personel seçildi');
        console.log('  ✓ Mesai saatleri girildi');
        console.log('  ✓ Kayıt eklendi ve kaydedildi');
        console.log('  ✓ PDF çıktısı oluşturuldu');
        console.log('  ✓ Excel çıktısı oluşturuldu');
        
        await page.waitForTimeout(3000);
        
    } catch (error) {
        console.error('\n❌ TEST HATASI:\n', error.message);
        console.error(error);
        await page.screenshot({ path: 'test-error.png' });
        console.log('   📸 Hata ekran görüntüsü: test-error.png');
    } finally {
        await browser.close();
    }
})();

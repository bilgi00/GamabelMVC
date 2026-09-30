# Uygulama İyileştirme Önerileri

GAMABELMVC için önerilen geliştirmeler önem sırasına göre listelenmiştir. İlk aşamadaki hedef, yetki ve oturum davranışını doğrulamak; sonraki aşamada veri değiştiren istekleri ve operasyonel güvenliği güçlendirmektir.

## 1. Yetki Testlerini Otomatikleştirme

PRS ve STS için rol bazlı erişim testleri ekleyin. Her rol için hem izinli hem de yasaklı sayfaları ve işlemleri doğrulayın.

- Oturumsuz kullanıcı korumalı URL'ye erişememeli; ilgili giriş ekranına yönlenmeli.
- PRS ve STS oturumları birbirinin sayfalarını açamamalı.
- Her rol yalnızca izin verilen sayfa ve işlemlere erişebilmeli.
- Birim amiri başka birimin; şube personeli başka şubenin verisini okuyamamalı veya değiştirememeli.
- Oturum sona erdikten veya kullanıcı çıkış yaptıktan sonra korumalı içerik tekrar açılamamalı.
- Menüde bağlantısı gizlenen sayfaya doğrudan URL yazarak erişim mümkün olmamalı.
- AJAX isteklerinde beklenen `401` ve `403` cevapları doğrulanmalı.

## 2. POST İşlemlerinde CSRF Koruması

Veri oluşturan, değiştiren veya silen tüm form ve API isteklerini gözden geçirin. Çıkış gibi güvenlik açısından önemli işlemler dahil, GET isteği veri değiştirmemelidir.

- MVC POST action'larında anti-forgery doğrulaması kullanın.
- Razor form tag helper'larının token ürettiğini doğrulayın.
- AJAX/fetch çağrılarında anti-forgery token'ı istekle gönderin.
- Token eksik veya geçersiz olduğunda isteğin reddedildiğini test edin.
- Proje için uygunsa genel `AutoValidateAntiforgeryToken` filtresi uygulayın; istisnaları açıkça belgeleyin.

## 3. Geliştirme Ortamındaki Test Kullanıcısı Seed İşlemi

`Program.cs` geliştirme ortamında uygulama başlarken `test` kullanıcısını silip yeniden oluşturuyor. Bu davranış gerçek geliştirme verisini beklenmedik şekilde değiştirebilir.

- Otomatik silme/ekleme işlemini uygulama başlangıcından çıkarın.
- Gerekliyse açıkça çalıştırılan, tekrarlanabilir bir seed komutuna veya development-only araca taşıyın.
- Seed işleminin yalnızca yerel/test veritabanına bağlandığını doğrulayın.
- Üretim ortamında test hesabı oluşturulmadığını test edin.

## 4. Oturum Süresi ve Çıkış Doğrulaması

Mevcut 15 dakikalık hareketsizlik ve 8 saatlik mutlak oturum süresinin uygulama davranışını gerçek isteklerle test edin.

- Hareketsizlik süresi dolunca sonraki istek login ekranına gitmeli.
- Mutlak oturum süresi, aktif kullanım olsa bile yeniden giriş istemeli.
- Çıkış Session ve kimlik doğrulama cookie'sini iptal etmeli.
- Çıkış sonrası tarayıcı geri tuşu ile hassas içerik gösterilmemeli.
- AJAX istekleri login HTML'i yerine beklenen durum kodunu almalı.
- Uygulama yeniden başlatıldığında oturumların beklenen şekilde geçersizleştiği doğrulanmalı.

## 5. Parola ve Hesap Açma Güvenliği

Bu konu önceki güvenlik değişikliklerinin kapsamı dışında bırakılmıştı; ayrı ve planlı bir güvenlik işi olarak ele alınmalıdır. PRS tarafında parolalar düz metinle doğrulanıyor ve kayıt akışı anonim hesap oluşturabiliyor.

- Parolaları uygun bir parola hash algoritmasıyla saklayın; düz metin parola saklamayı ve karşılaştırmayı kaldırın.
- Mevcut kullanıcı parolaları için güvenli geçiş planı oluşturun.
- Hesap açma ve etkinleştirme süreçlerini yetkili onayına bağlayın.
- Yeni hesapların rol ve birim yetkisini kullanıcı girdisinden doğrudan almayın.
- Başarısız girişlere hız sınırı ve geçici hesap kilidi ekleyin.
- Kimlik bilgilerini loglara yazmayın.

## 6. Hata Mesajları ve Loglama

Kullanıcıya gösterilen hata ile operatörün inceleyeceği ayrıntılı hata kaydını ayırın.

- Veritabanı/uygulama istisnalarının içeriğini kullanıcıya doğrudan göstermeyin.
- Kullanıcıya genel ve güvenli bir hata mesajı gösterin.
- Ayrıntıları sunucu loguna; istek kimliği, zaman ve ilgili kullanıcı bilgisiyle kaydedin.
- Başarılı/başarısız giriş, çıkış, rol değişikliği ve kritik yönetim işlemleri için denetim izi tutun.
- Parola, session token ve hassas kişisel verileri loglamayın.

## 7. Geliştirme Komutlarının Güvenilirliği

Son çalışma bağlamında `dev-tools.ps1 full` komutu çıkış kodu `1` ile tamamlanmış görünüyor. Başarısız adımı ve hata çıktısını inceleyip aracın build/test adımlarını tekrarlanabilir hale getirin.

- Script'in hangi alt adımda başarısız olduğunu açıkça yazdırın.
- Hata durumunda sıfır olmayan çıkış kodu döndürün; başarılı tamamlanmada sıfır kodu kullanın.
- Build ve test komutlarını mümkün olduğunca CI ile aynı şekilde çalıştırın.
- Güvenlik davranışları için otomatik testleri geliştirme kontrol listesine ekleyin.

## Önerilen Uygulama Sırası

1. Yetki ve oturum senaryoları için testleri ekleyin.
2. Veri değiştiren tüm isteklerde CSRF korumasını tamamlayın.
3. Geliştirme ortamındaki otomatik test kullanıcısı seed işlemini güvenli hale getirin.
4. Oturum süresi ve çıkış davranışını doğrulayın.
5. Parola ve hesap açma akışını ayrı değişiklik olarak planlayın.
6. Hata mesajları ve güvenlik loglarını iyileştirin.
7. `dev-tools.ps1 full` başarısızlığını inceleyin.

> Her madde tamamlandığında ilgili testleri çalıştırın ve sonucu doğrulayın. Özellikle rol/kapsam testleri, parola geçişi ve üretim seed davranışı doğrulanmadan güvenlik işi tamamlanmış sayılmamalıdır.

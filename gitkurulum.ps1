# Git kullanıcı bilgileri
git config --global user.name "bilgi00"
git config --global user.email "bilgi00@gmail.com"

# D:\ sürücüsüne geç
Set-Location D:\

# Proje zaten varsa güncelle, yoksa indir
if (Test-Path "GamabelMVC") {
    Set-Location "GamabelMVC"
    git pull
}
else {
    git clone https://github.com/bilgi00/GamabelMVC.git
    Set-Location "GamabelMVC"
}

# Bağımlılıkları yükle
dotnet restore

# Derle
dotnet build

# VS Code aç
code .
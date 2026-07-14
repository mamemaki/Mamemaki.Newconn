$cert = New-SelfSignedCertificate -KeyAlgorithm RSA -KeyLength 2048 -DnsName "test.local" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1,1.3.6.1.5.5.7.3.2") -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(50)
$password = ConvertTo-SecureString -String "testcert" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "testcert.pfx" -Password $password

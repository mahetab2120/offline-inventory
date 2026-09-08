namespace Security.Constants;

public static class SecurityConstants
{
    // The AFS Super Admin Public Key embedded into all distributed Business Applications
    // Used by Business App to verify AFS signatures on Data Keys (.key) and Licenses (.lic)
    public const string EmbeddedCaPublicKeyPem = 
@"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAsjnAwt4nRJ3USEEorNWm
KItDvuCAu6mOKQNoqKG7IygrPW7qgHnHJTBejn6/FBgN2klbthub1J0YvzAMSezB
ejm83jX+9TfYreaXIZtJjO2eB3akq5b+eNNtw4stPeolRwIQInGPtWnpmQnki2sc
1uowm7S0Oe/SL88kGmnMys9vh3cL1JSLuK+oLeGuTGaJc9PcGTzvB9AjyqLdmBIv
7mcs+X9Z7/PUxlgBmIofVtmEjffIcRcXbBc3O/imcDr+7xeEETIIsughNPAuYMGX
q5FM+fByCLZ8BtYSTIex0AM7vGULg8vwocngs/cUErLC6jzO/44qjHo1VSVZfIq+
WQIDAQAB
-----END PUBLIC KEY-----";

    public const string MasterCaPublicKeyPem = EmbeddedCaPublicKeyPem;

    // Master AFS Private Key used by AFS Super Admin Portal to sign and issue keys
    public const string MasterCaPrivateKeyPem = 
@"-----BEGIN PRIVATE KEY-----
MIIEugIBADANBgkqhkiG9w0BAQEFAASCBKQwggSgAgEAAoIBAQCyOcDC3idEndRI
QSis1aYoi0O+4IC7qY4pA2ioobsjKCs9buqAecclMF6Ofr8UGA3aSVu2G5vUnRi/
MAxJ7MF6ObzeNf71N9it5pchm0mM7Z4HdqSrlv54023Diy096iVHAhAicY+1aemZ
CeSLaxzW6jCbtLQ579IvzyQaaczKz2+HdwvUlIu4r6gt4a5MZolz09wZPO8H0CPK
ot2YEi/uZyz5f1nv89TGWAGYih9W2YSN98hxFxdsFzc7+KZwOv7vF4QRMgiy6CE0
8C5gwZerkUz58HIItnwG1hJMh7HQAzu8ZQuDy/ChyeCz9xQSssLqPM7/jiqMejVV
JVl8ir5ZAgMBAAECgf9hdzPzXYob/DQbT4bu/efgREjIGf5Mom0cAME7dzbgAxei
gQW9PNuresg7JRVZ582rknKVJIQTwdXGuH//7XnhXbHr05uafvAAKhp8Rm/yof/K
FRf4vppreTSNu48CTQoVywsHyxLMIx+ckcxqcilTXr82Z5woEv7fJmiYCUP1pJFD
ZSayjNs2bm1tsaSTsAfkR7TNsjToGGhAvBTtn/UbBaVWYqne0ltQahhAvXtWGZhV
0QsuTaYzNqzvamUFacY33dxeZY/5VbkKOPrBhgB+FRe35f3mjWQXEIejgFGWNQDx
NWMQzBBolj7ZPUd2ookWOlunf0AF8bpIOGNNqHECgYEA42xl7oVEJ5498RDQmlNP
lJaaotR+Ul6B3j64xhkZKhjSCOPSQrvSsCoU+RfYWKuEJIBS3yvA5umn5CxMBcTu
ujZT+pbGJei+PUhvI2L7n2YR/XJ7i0W5ZmYOC2Vy+XlB+B5nSpAXH4rBPCxMTclS
GIX3JMJo08qUwOYy4ronr08CgYEAyJ7K3vgWsVZC1KCAfYjxRhcrVItStSjA5dL4
G8T+t7LgkSQ/X+D014/hXw3L3Hjl9TNKnk3YVCkV87QUQMjoEjIbKfZLGPgQ2ERg
F9SgAza27SaPCAsi84oBaFwNe9XLJ+pnNXqvGAc2/VVotl96RIAiG9NaitWnvSmO
fvHYjdcCgYBiHXf0aYY32Ws7v1df3SVuI3NfYHGHM8KvkTvCKz3SDZc/wpoJtGJ3
IhCeTo1F4+lEniAirAuzE4cdR4cczhN4PswIRlgCLuE0KzXXfHK2GCEWyPdH5LLR
3KGehQwPWSL+2o8RegyfzQsE3M+ml35VmiY/s6fqB3IZrraxXkKUvQKBgH1ARkA2
e1R0Gn1NR2sYmCm+RVsfMJ/RtbzGnggYUT62+uUi0D434CTEu1vw7RnUkR0ozKlQ
yIKitAXWo95ekCTsC3GDRxdrqHidF7FJGi1nd4VP0XSgH04VFxPkhLaPn6pn+c+1
rKM/veEj9aAGs/sYVDMzHRAYnATJcSFoNF85AoGAbFPDsic2DitSrLwJBNIzRxZu
fTqP/DDvr1kmz0/PrtThPrzC4bs/fGA0EU+86T7Rgd/0jK1jks8zbOt68B15V69p
E8uCGygQ4YhuC6G6nnFS6vP4VKUhKrM9AfFoqavok7LG7QRRF/tpg/3Pwl8pp/0o
awFu/xgg8ACOWYW/y8Q=
-----END PRIVATE KEY-----";

    public const string DataKeyPackageType = "DATA_KEY";
    public const string LicensePackageType = "LICENSE";
    public const string AuditExportPackageType = "AUDIT_EXPORT";
}

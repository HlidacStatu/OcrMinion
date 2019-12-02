# OcrMinion

Klient pro dolování textů z obrázkových dokumentů (OCR) pro Hlídač státu.
[Najdete také na docker hubu](https://hub.docker.com/r/hlidacstatu/ocrminion)

## Aktuality

2\. 12. 2019 - Spustili jsme novou verzi klienta. Aktuální verze je `ocrminion:lin-v2.0`. Aktualizujte si prosím své kontejnery.

Aktualizaci provedete spuštěním následujících příkazů:
> :warning: **Nezapomeňte si opět nastavit váš api klíč a email v příkazu docker run**  

``` shell
docker stop minion
docer container rm minion
docker run --name minion -d -e OCRM_APIKEY=mykey -e OCRM_EMAIL=muj@mail.cz hlidacstatu/ocrminion:lin-v2.0
```

Případně pokud chcete detailnější logy použíjte následující příkaz:

``` shell
docker run --name minion -d -e OCRM_APIKEY=mykey -e OCRM_EMAIL=muj@mail.cz -e Logging__LogLevel__Default=Information hlidacstatu/ocrminion:lin-v2.0
```

## Statistiky, aktuálně běžící klienti

Přehled aktuálně běžících klientů a žebříček nejpracovitějších je na Hlídači státu: [www.hlidacstatu.cz/api/v1/ocrstat](https://www.hlidacstatu.cz/api/v1/ocrstat).

## Jak OcrMinion spustit

### Požadavky

1) Mít [nainstalovaný docker](https://docs.docker.com/install/). Pokud používáte Docker ve Windows, je potřeba ho mít přepnutý na [Linuxové kontejnery](https://docs.docker.com/docker-for-windows/#switch-between-windows-and-linux-containers).

2) Ke spuštění budete potřebovat API klíč, který [získáte po registraci na Hlídači státu](https://www.hlidacstatu.cz/api).

3) Pak už stačí jen v terminálu/příkazové řádce spustit správný příkaz.

### Základní spuštění

Pokud se v tom nechcete moc hrabat, pak vám bude stačit v následujícím příkaze nahradit hodnotu "mykey" klíčem.

```  sh
docker run --name minion -d -e OCRM_APIKEY=mykey hlidacstatu/ocrminion:ocrminion:lin-v2.0
```

### Doporučené spuštění

Kvůli [síni slávy](https://www.hlidacstatu.cz/api/v1/ocrstat) budeme rádi, když spustíte docker s následujícím příkazem, kde vyplníte svůj email. Nebojte se - emaily nebudeme zveřejňovat.  

V náledujícím příkaze nahraďte "mykey" klíčem, který od nás obdržíte. Hodnotu "muj@email.cz" nahraďte svým emailem.

```  sh
docker run --name minion -d -e OCRM_APIKEY=mykey -e OCRM_EMAIL=muj@mail.cz hlidacstatu/ocrminion:ocrminion:lin-v2.0
```

## Environment variables

`OCRM_APIKEY` - Nastavte hodnotu (bez mezer), kterou dostanete od nás. API key získáte na [www.hlidacstatu.cz/api](https://www.hlidacstatu.cz/api)  
`OCRM_EMAIL` - Nastavte vlastní hodnotu (bez mezer), jak chcete být identifikováni serverem. Ideálně svůj email.  
`Logging__LogLevel__Default` - **Debug|Information|Warning** - defaultní hodnota je nastavena na Warning. To znamená, že uvidíte jen chyby. Pokud chcete detailnější informace, poté nastavte na hodnotu **Information**. Pokud chcete velmi podrobné informace, tak nastavte na **Debug**.  

## Jak zastavit a spustit znovu

### Zastavení docker kontejneru

Pokud potřebujete uvolnit systémové prostředky, tak docker zastavíte pomocí příkazu:  

``` sh
docker stop minion
```  

### Spuštění docker kontejneru

Pokud chcete spustit už jednou zastavený balíček, tak k tomu použijte následující příkaz:  

``` sh
docker start minion
```  

> :warning: **Pro spuštění zastaveného kontejneru už nepoužívejte příkaz `docker run`. Zbytečně byste vytvořili další instanci!**  


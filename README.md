# OcrMinion

Klient pro získávání textů z obrázkových dokumentů pro Hlídač státu.

## Jak ho spustit

### Požadavky

Je potřeba mít [nainstalovaný docker](https://docs.docker.com/install/).

Pak už stačí jen v terminálu/příkazové řádce spustit správný příkaz.

### Základní spuštění

Pokud se v tom nechcete moc hrabat, pak vám bude stačit v následujícím příkaze nahradit hodnotu "mykey" klíčem, který od nás dostanete.

```  sh
docker run --name minion -d -e OCRM_APIKEY=mykey hlidacstatu/ocrminion:latest
```

### Doporučené spuštění

Rádi bychom založili nějakou "síň slávy", proto budeme rádi, když spustíte docker s následujícím příkazem, kde vyplníte svůj email. Nebojte se - emaily nebudeme zveřejňovat.  

V náledujícím příkaze nahraďte "mykey" klíčem, který od nás obdržíte. Hodnotu "muj@email.cz" nahraďte svým emailem.

```  sh
docker run --name minion -d -e OCRM_APIKEY=mykey -e OCRM_EMAIL=muj@mail.cz hlidacstatu/ocrminion:latest
```

## Environment variables

`OCRM_APIKEY` - Nastavte hodnotu (bez mezer), kterou dostanete od nás.  
`OCRM_EMAIL` - Nastavte vlastní hodnotu (bez mezer), jak chcete být identifikováni serverem. Ideálně svůj email.  
`OCRM_DEMO` - **true|false** - defaultní hodnota je **false**. Tato hodnota slouží pouze pro testovací účely.  
`Logging__LogLevel__Default` - **Debug|Information|Warning** - defaultní hodnota je Information. Pokud chcete detailnější informace, poté nastavte na hodnotu **Debug**. Pokud chcete minimální informace, tak nastavte na **Warning**.  

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


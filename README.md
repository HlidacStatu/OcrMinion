# OcrMinion

Client pro získávání textů z obrázkových dokumentů pro Hlídač státu

## Jak spustit

Pokud se v tom nechcete moc hrabat, pak vám bude stačit v následujícím příkaze nahradit hodnotu "mykey" klíčem, který od nás dostanete.

```  bash
$ docker run -d -e OCRM_APIKEY=mykey hlidacstatu/ocrminion:latest
```

## Environment variables

`OCRM_APIKEY` - Nastavte hodnotu (bez mezer), kterou dostanete od nás.  
`OCRM_SERVER` - Nastavte vlastní hodnotu (bez mezer), jak chcete být identifikováni serverem.  
`OCRM_DEMO` - **true|false** - defaultní hodnota je **false**. Tato hodnota slouží pouze pro testovací účely.  
`Logging__LogLevel__Default` - **Debug|Information|Warning** - defaultní hodnota je Information. Pokud chcete detailnější informace, poté nastavte na hodnotu **Debug**. Pokud chcete minimální informace, tak nastavte na **Warning**.  

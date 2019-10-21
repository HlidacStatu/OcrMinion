#!/bin/sh
while [ 1 -eq 1 ]
do
  if pgrep -x "dotnet" >/dev/null
  then
    echo "app is running"
  else
    echo "app is down"
    dotnet OcrMinion.dll
  fi
  sleep 10
done
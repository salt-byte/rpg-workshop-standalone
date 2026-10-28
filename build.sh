#!/bin/sh
# 把 rpg-workshop.html（claude.ai 版）包成独立的 index.html；两边是同一份代码
cd "$(dirname "$0")"
{
  printf '<!doctype html>\n<html lang="zh-CN">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n</head>\n<body>\n'
  cat ../rpg-workshop.html
  printf '\n</body>\n</html>\n'
} > index.html
echo "built index.html ($(wc -c < index.html) bytes)"

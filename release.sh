#!/bin/zsh
# Публикация новой версии игры на GitHub Releases.
# Использование:  ./release.sh 1.2 "Что нового в этой версии"
# Игра на телефоне сама увидит новую версию и предложит «ОБНОВИТЬ».
set -e
VER="$1"
NOTES="${2:-Исправления и улучшения}"
[ -z "$VER" ] && { echo "Укажите версию, например: ./release.sh 1.2 \"Что нового\""; exit 1; }

HERE=~/CODE/gad-22x
GH=~/CODE/hay-hunter/tools/gh/bin/gh
U=/Applications/Unity/Hub/Editor/6000.3.21f1/Unity.app/Contents/MacOS/Unity
REPO_NAME=gad-22x

LOGIN=$($GH api user -q .login)
REPO="$LOGIN/$REPO_NAME"
$GH repo view "$REPO" >/dev/null 2>&1 || { echo "Репозиторий $REPO не найден"; exit 1; }

# Where the game looks for updates (baked into the build).
printf "%s" "https://github.com/$REPO/releases/latest/download/" > $HERE/UnityProject/Assets/Resources/update_base.txt

CODE=$(python3 -c "import sys; p=(sys.argv[1].split('.')+['0','0'])[:3]; print(int(p[0])*10000+int(p[1])*100+int(p[2]))" "$VER")
echo "Сборка версии $VER (код $CODE)…"
cd $HERE/UnityProject
$U -batchmode -nographics -projectPath "$PWD" -executeMethod BuildScript.BuildAndroid -appVersion "$VER" -versionCode "$CODE" -quit -logFile $HERE/release.log
grep -E "\[Build\] Android: Succeeded" $HERE/release.log >/dev/null || { echo "Сборка не удалась — см. release.log"; exit 1; }

OUT=$HERE/release/$VER
mkdir -p $OUT
cp Builds/Android/GAD22X.apk $OUT/GAD22X.apk
cp Builds/Android/GAD22X.apk $HERE/GAD22X.apk
# size + sha256 let the game reject a broken download before Android's installer sees it.
python3 - "$VER" "$NOTES" "$OUT/version.json" "$OUT/GAD22X.apk" <<'PY'
import hashlib, json, os, sys
apk = sys.argv[4]
sha = hashlib.sha256(open(apk, "rb").read()).hexdigest()
json.dump({"version": sys.argv[1], "notes": sys.argv[2], "apk": "GAD22X.apk", "size": os.path.getsize(apk), "sha256": sha},
          open(sys.argv[3], "w"), ensure_ascii=False)
PY

$GH release create "v$VER" "$OUT/GAD22X.apk" "$OUT/version.json" --repo "$REPO" --title "ГАД 2-2 X $VER" --notes "$NOTES"
echo "Готово: https://github.com/$REPO/releases/tag/v$VER"

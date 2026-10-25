#!/bin/zsh
# Сборка для Mac + автотест.  Использование: ./build_and_test.sh <папка_для_скриншотов>
set -e
cd ~/CODE/gad-22x/UnityProject
U=/Applications/Unity/Hub/Editor/6000.3.21f1/Unity.app/Contents/MacOS/Unity
LOG=~/CODE/gad-22x/buildmac.log
$U -batchmode -nographics -projectPath "$PWD" -executeMethod BuildScript.BuildMac -quit -logFile $LOG || true
grep -E "error CS" $LOG | sort -u | head -20
grep -E "\[Build\]" $LOG | tail -1
SH=${1:A}; [[ $1 = /* ]] || SH=~/CODE/gad-22x/$1; rm -rf $SH; mkdir -p $SH
"./Builds/Mac/GAD22X.app/Contents/MacOS/Herbal" -autotest -shots $SH -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile $SH/player.log || true
cat $SH/autotest.txt 2>/dev/null
grep -E "Exception|NullReference" $SH/player.log | sort | uniq -c | head -10

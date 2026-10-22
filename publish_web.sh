#!/bin/zsh
# Публикация браузерной версии игры на GitHub Pages.
# Использование:  ./publish_web.sh   (после сборки BuildScript.BuildWebGL → UnityProject/Builds/WebGL)
# Ссылка: https://<логин>.github.io/gad-22x/
set -e
HERE=~/CODE/gad-22x
GH=~/CODE/hay-hunter/tools/gh/bin/gh
SRC=$HERE/UnityProject/Builds/WebGL
[ -f $SRC/index.html ] || { echo "Нет сборки $SRC — сначала соберите BuildScript.BuildWebGL"; exit 1; }
LOGIN=$($GH api user -q .login)
REPO="$LOGIN/gad-22x"

# Each publish is a single fresh commit on gh-pages (force-pushed), so old builds don't pile up in the repo.
DIR=$HERE/web-publish
rm -rf $DIR; mkdir -p $DIR
cp -R $SRC/ $DIR/
touch $DIR/.nojekyll
cd $DIR
git init -q -b gh-pages
git add -A
git -c user.name="$LOGIN" -c user.email="$LOGIN@users.noreply.github.com" commit -q \
  -m "Браузерная версия $(date +%Y-%m-%d\ %H:%M)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git -c credential.helper= -c credential.helper="!$GH auth git-credential" push -q -f "https://github.com/$REPO.git" gh-pages
$GH api "repos/$REPO/pages" >/dev/null 2>&1 || $GH api -X POST "repos/$REPO/pages" -f "source[branch]=gh-pages" -f "source[path]=/" >/dev/null
echo "Готово: https://$LOGIN.github.io/gad-22x/"

# AlembicToVAT

## 概要
UnityでAlembicからVATを生成するエディタ拡張です

## 使い方
* AlembicをSceneに配置する
* ToolBarで以下を選択してエディタ拡張Windowを開く
```
metaaa -> AlembicToVAT 
```
* Alembicやその他項目を設定しProcessボタンを押す

## フォーマット
X軸は頂点ID、Y軸はフレーム 左下(0, 0)がスタート<br>
頂点数がテクスチャの幅を超える場合改行<br>
sRGBHalf <br>
トポロジーが変化するものも対応 <br>

## UPM install
```
https://github.com/metaaaa/AlembicToVAT.git?path=Assets/AlembicToVAT
```

## 参考リンク

Unity-AlembicToVAT<br>
https://github.com/Gaxil/Unity-AlembicToVAT

Animation-Texture-Baker<br>
https://github.com/sugi-cho/Animation-Texture-Baker

何でも出せる万能エクスポーター VAT で Houdini の可能性が100億倍広がる - Unity道場2020 2月<br>
https://www.youtube.com/watch?v=qXcxBw3KUtw

[Blender] 初心者のためのGeometry Nodes入門 / Intro to Geometry Nodes for Beginners <br>
https://www.youtube.com/watch?v=yQgfsVy62Sw

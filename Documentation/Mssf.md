Music Sheet Sound File(MSSF)
==================

MSSF は、16ビット値 × 32 のサンプルおよび4値のエンベロープ、ステレオパンポットを格納できるフォーマットです。

もともと、ソフトウェアMIDIシンセサイザー「Music Sheet」上で楽器パートの音色を定義するためにつくられました。

公式エディタでは、16階調(4ビット)でのみ波形を編集できるようになっていますが、内部的には16ビットで保持されています。

ファイル仕様
-----

MSSF には、現時点でバージョン1とバージョン2が存在します。

### バージョン1

|型|バイト数|説明|
|--|--|--|
|文字の配列|11|"MSSF_VER001"|
|符号つき16ビット整数の配列|64|波形データ|
|符号付き32ビット整数|4|エンベロープ:アタックタイム|
|符号付き32ビット整数|4|エンベロープ:ディケイタイム|
|符号なし8ビット整数|1|エンベロープ:サスティンレベル|
|符号付き32ビット整数|4|エンベロープ:リリースタイム|
|符号付き32ビット整数|4|パンポット|

最初にマジックナンバーがあります。無駄が多かったかもしれません。
そして、32個の16ビット整数、次に4つのエンベロープパラメーター(よくADSRと呼ばれます)がきます。サスティンレベルのみ8ビットなので注意！
最後に32ビットでパンポットがきます。

### バージョン2

バージョン1の仕様に加え、ノイズ設定が追加されています。

MIDI規格として一般的なGM規格では、ティンパニや太鼓など、パーカッションの楽器も存在します。
そのような音源を実現するために急いでつくられましたが、1周期分の波形を利用しているMSSFでノイズを発生させるのは限界があり、実装したものの残念な音声になってしまいました。

そのため、ノイズ設定は使用する必要性がないかもしれません...

|型|バイト数|説明|
|--|--|--|
|文字の配列|11|"MSSF_VER002"|
|符号つき16ビット整数の配列|64|波形データ|
|符号付き32ビット整数|4|エンベロープ:アタックタイム|
|符号付き32ビット整数|4|エンベロープ:ディケイタイム|
|符号なし8ビット整数|1|エンベロープ:サスティンレベル|
|符号付き32ビット整数|4|エンベロープ:リリースタイム|
|符号付き32ビット整数|4|パンポット|
|符号なし8ビット整数|1|ノイズ設定: 0がナシで、1が長周期ノイズ、2が短周期ノイズです|

処理系の仕様
------------

MSSF ver2は正直黒歴史のようになっているのであまりオススメしません...

### 共通

MSSF は今後更新しない方針です。作るとしたら新しい規格つくります。なので、バージョンは無視できるものとし、リーダーではバージョン番号を無視し、ライターでは任意の形式でどうぞ。

MSSF はリトルエンディアン形式で格納されています。

### リーダー

マジックナンバーがバージョンごとに違いますが、MSSF_VERをマジックナンバーとして判断し、あとの3バイトは無視してください。

ver1の分読み込み終わったら、それ以降がver2の拡張なので読まずに閉じて構いません。

### ライター
ver1 ver2 どちらの形式で格納しても構いません。ただし、マジックナンバーのバージョンを無視しない実装もあるので、一応規格には合わせてください。


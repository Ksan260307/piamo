# Blender アセット（けんばん）

`scripts/build_piano_kit.py` が、アプリで使う けんばんと ピアノのケースを プロシージャルに作ります。

```bash
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python blender/scripts/build_piano_kit.py
```

- 出力: `unity/Assets/Art/Models/*.fbx`（Unity 用）と `blender/piano_kit.blend`（編集・確認用。1 オクターブに組み立ててある）
- 単位: 1 = 白鍵 1 本ぶんのはば。座標は X = ならび（右へ）、Y = 奥、Z = 上（Unity では X・Z・Y）
- 寸法は Unity の `PianoDimensions` と同じ値（変えたら両方なおす）

| 形 | 中身 |
| --- | --- |
| `Key_White_Full` / `_L` / `_R` / `_LR` | 白鍵。となりに黒鍵があるところだけ 奥を切りかく（ド・ファ は右、ミ・シ は左、レ・ソ・ラ は両がわ。黒鍵なしの せってい では Full） |
| `Key_Black` | 黒鍵（上がすぼまり、手前の面がななめ） |
| `Case_End_L` / `_R` | 左右の そで（丸いかどの箱） |
| `Case_Back` / `Case_Front` / `Case_Felt` / `Case_Bed` | 奥の板・手前のふち・赤いフェルト・鍵盤の下の台。X が 0〜1 の長さ 1 で、Unity で鍵盤の数だけ のばす |

- 色は Unity のマテリアル（`Piamo/ToyLit`）で つけます。頂点カラー `Col` には、すき間の暗さ（下ほど暗い）だけを入れています
- `scripts/render_preview.py` で、アプリと同じくらいの向きから見た確認用の絵を作れます（`blender/previews/`）
- テスト: `blender -b --factory-startup --python-exit-code 1 --python blender/tests/test_piano_kit.py`

# testdata/smoke

CI の GUI 起動スモーク（`.github/workflows/ci.yml` の `gui-smoke`、`release.yml` の `build`）が
publish 直後の実行ファイルに食わせる最小サンプル。`testdata/real/` は生成物で git 管理外のため、
ここだけは小さな実ファイルをコミットしている。

- `sample.png` — 4x4 の PNG（`generate-real-files.sh` の PNG と同じもの）

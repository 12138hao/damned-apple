extends SceneTree

# 连通域分析：把贴图按 4 连通拆成独立的岛屿，打印每个的包围盒和像素数。
# 用途：从苹果怪贴图里分离出「身体 / 左手 / 右手」。

func _load(path: String) -> Image:
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		print("打不开 ", path); return null
	var img := Image.new()
	if img.load_png_from_buffer(f.get_buffer(f.get_length())) != OK:
		print("解码失败 ", path); return null
	if img.is_compressed(): img.decompress()
	img.convert(Image.FORMAT_RGBA8)
	return img

func _initialize() -> void:
	var dir := "D:/workbuddyhuancun/2026-11-05-15-03-54/pixel-apple/"
	var img := _load(dir + "apple_monster_pixel160_160x159.png")
	if img == null: quit(); return

	var w := img.get_width()
	var h := img.get_height()
	var data := img.get_data()          # PackedByteArray，按字节索引，避免 get_pixel 太慢
	var seen := PackedByteArray()
	seen.resize(w * h)
	seen.fill(0)

	var comps := []
	for sy in range(h):
		for sx in range(w):
			var idx := (sy * w + sx) * 4
			if data[idx + 3] < 13: continue        # 几乎全透明
			if seen[sy * w + sx] != 0: continue

			var stack := [Vector2i(sx, sy)]
			seen[sy * w + sx] = 1
			var mnx := sx; var mxx := sx
			var mny := sy; var mxy := sy
			var count := 0
			while stack.size() > 0:
				var p: Vector2i = stack.pop_back()
				count += 1
				mnx = min(mnx, p.x); mxx = max(mxx, p.x)
				mny = min(mny, p.y); mxy = max(mxy, p.y)
				for d in [Vector2i(1,0), Vector2i(-1,0), Vector2i(0,1), Vector2i(0,-1)]:
					var nx: int = p.x + d.x
					var ny: int = p.y + d.y
					if nx < 0 or ny < 0 or nx >= w or ny >= h: continue
					if seen[ny * w + nx] != 0: continue
					if data[(ny * w + nx) * 4 + 3] < 13: continue
					seen[ny * w + nx] = 1
					stack.append(Vector2i(nx, ny))

			if count > 30:   # 忽略噪点
				comps.append({"box": Rect2i(mnx, mny, mxx - mnx + 1, mxy - mny + 1), "n": count})

	comps.sort_custom(func(a, b): return a["n"] > b["n"])
	print("=== 连通域 ", comps.size(), " 个（像素数从大到小）")
	for c in comps:
		var b: Rect2i = c["box"]
		print("   x[%d, %d]  y[%d, %d]  宽 %d 高 %d  像素 %d" % [
			b.position.x, b.position.x + b.size.x - 1,
			b.position.y, b.position.y + b.size.y - 1,
			b.size.x, b.size.y, c["n"]])

	quit()

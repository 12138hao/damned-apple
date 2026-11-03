extends SceneTree
# 生成主菜单天空装饰素材：太阳、光晕光线、两种云。
# 全部程序化绘制（像素风），不依赖外部图片。
# 跑法：godot --headless --path . --script res://tools/gen_sky_assets.gd

const OUT := "res://assets/ui/"


func _initialize() -> void:
	_gen_sun()
	_gen_cloud("cloud_a.png", 112, 56, [
		Vector3(30, 34, 17), Vector3(54, 26, 21), Vector3(80, 32, 18),
		Vector3(46, 40, 14), Vector3(68, 42, 13), Vector3(92, 42, 10),
	])
	_gen_cloud("cloud_b.png", 80, 40, [
		Vector3(24, 24, 13), Vector3(46, 20, 15), Vector3(62, 26, 11),
	])
	print("[Sky] 素材生成完毕")
	quit()


# ── 太阳：中心亮芯 + 六圈递减光晕，硬边像素 ────────────────────
# 64×64 是为了让最外圈的 alpha 在贴图边缘前降到接近 0，
# 否则放大后会看到一圈方形的硬边。
func _gen_sun() -> void:
	var s := 64
	var img := Image.create_empty(s, s, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	var cx := 32.0
	var cy := 32.0
	# [半径上限, 颜色]，从内到外
	var bands := [
		[11.0, Color8(255, 247, 220, 255)],
		[15.0, Color8(255, 232, 168, 255)],
		[19.0, Color8(255, 212, 124, 240)],
		[23.0, Color8(255, 190, 92, 150)],
		[27.0, Color8(255, 172, 66, 70)],
		[30.0, Color8(255, 160, 54, 24)],
	]

	for y in range(s):
		for x in range(s):
			var d := Vector2(x + 0.5 - cx, y + 0.5 - cy).length()
			for b in bands:
				if d <= float(b[0]):
					img.set_pixel(x, y, b[1])
					break

	img.save_png(OUT + "sun.png")
	print("  sun.png %dx%d" % [s, s])


# ── 云：几团圆叠成轮廓，底部压一道蓝灰阴影 ──────────────────────
func _gen_cloud(file: String, w: int, h: int, puffs: Array) -> void:
	var img := Image.create_empty(w, h, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	var mask := []
	mask.resize(w * h)
	for i in range(w * h):
		mask[i] = false

	for p in puffs:
		var px: int = int(p.x)
		var py: int = int(p.y)
		var pr: int = int(p.z)
		for y in range(max(0, py - pr), min(h, py + pr + 1)):
			for x in range(max(0, px - pr), min(w, px + pr + 1)):
				var ddx := x - px
				var ddy := y - py
				if ddx * ddx + ddy * ddy <= pr * pr:
					mask[y * w + x] = true

	var white := Color8(255, 255, 255, 255)
	var shade := Color8(214, 233, 247, 255)
	var under := Color8(186, 214, 236, 255)

	# 每列找最低点，往上三层做阴影，形成平底云
	var bottom := []
	bottom.resize(w)
	for x in range(w):
		bottom[x] = -1
		for y in range(h - 1, -1, -1):
			if mask[y * w + x]:
				bottom[x] = y
				break

	for x in range(w):
		var b: int = bottom[x]
		if b < 0:
			continue
		for y in range(h):
			if not mask[y * w + x]:
				continue
			var depth := b - y          # 距离底部几像素
			var col := white
			if depth == 0:
				col = under
			elif depth <= 2:
				col = shade
			img.set_pixel(x, y, col)

	img.save_png(OUT + file)
	print("  %s %dx%d" % [file, w, h])

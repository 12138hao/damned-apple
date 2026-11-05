extends SceneTree

# 程序化生成泥潭用的两个粒子贴图，像素风硬边，不抗锯齿。
#   bubble.png    冒泡：浅灰白小圆，带一点高光
#   mud_drop.png  飞溅泥块：不规则碎块，颜色对齐泥浆本体（近黑冷灰）
#
# 颜色依据：从 mud_cut_final.png 采样得到
#   泥体 rgb(14,13,15) / 表层 rgb(26,24,27) / 表面带 rgb(23,22,24)
#   所以泥块不能是棕褐色，必须是「近黑的冷灰」，只是比泥面亮一档才看得见。
#
# 跑法：--headless --path D:/h/my-game --script res://tools/gen_mud_assets.gd

const OUT := "res://assets/jungle/"


func _initialize() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	_gen_bubble()
	_gen_drop()
	print("[MudAssets] 生成完毕")
	quit()


# ── 泡泡：8×8，浅灰白，中心亮边缘透 ────────────────────────────
func _gen_bubble() -> void:
	var s := 8
	var img := Image.create_empty(s, s, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	var c := (s - 1) * 0.5
	for y in range(s):
		for x in range(s):
			var dx: float = x - c
			var dy: float = y - c
			var d: float = sqrt(dx * dx + dy * dy)
			if d > 3.6:
				continue

			var k: float = clamp(1.0 - (d / 3.6), 0.0, 1.0)
			var a: int = int(70.0 + 150.0 * k)

			var hl: bool = dx < -0.6 and dy < -0.6
			var col := Color8(232, 238, 230, a)
			if hl:
				col = Color8(252, 254, 250, 235)
			img.set_pixel(x, y, col)

	img.save_png(OUT + "bubble.png")
	print("  bubble.png %dx%d" % [s, s])


# ── 泥块：8×8 不规则碎块，冷灰（贴近泥浆但比泥体亮一档，否则在黑泥上看不见）──
func _gen_drop() -> void:
	var s := 8
	# 手工画的轮廓，不是圆 —— 泥浆溅起来的是碎块，圆点看着像水滴
	var mask := [
		[0, 0, 1, 1, 1, 0, 0, 0],
		[0, 1, 1, 1, 1, 1, 0, 0],
		[1, 1, 1, 1, 1, 1, 1, 0],
		[1, 1, 1, 1, 1, 1, 1, 1],
		[1, 1, 1, 1, 1, 1, 1, 1],
		[0, 1, 1, 1, 1, 1, 1, 1],
		[0, 0, 1, 1, 1, 1, 0, 0],
		[0, 0, 0, 1, 1, 0, 0, 0],
	]

	var img := Image.create_empty(s, s, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	for y in range(s):
		for x in range(s):
			if mask[y][x] == 0:
				continue

			# 是不是轮廓像素：上下左右只要有一个是空的，就算
			var edge := false
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var nx: int = x + d.x
				var ny: int = y + d.y
				if nx < 0 or ny < 0 or nx >= s or ny >= s or mask[ny][nx] == 0:
					edge = true
					break

			# 内部暗（贴泥体），轮廓亮（受光）。
			# 泥浆本身是近黑的，泥块如果整体也黑，在泥面上就完全看不见了 ——
			# 所以靠一圈略亮的轮廓把形状勾出来，颜色仍然是冷灰不带黄。
			#
			# 但轮廓不能太亮：之前用 190 的浅灰勾边，8×8 放大到 4~9 像素后
			# 看着像一个个空心小圆圈（气泡），不像泥块。改成 86 的中暗灰，
			# 只在左上角补一小块高光当受光面，才有「实心泥团」的重量感。
			var col := Color8(86, 82, 90, 255) if edge else Color8(56, 53, 60, 255)
			if not edge and x + y <= 3:
				col = Color8(128, 124, 132, 255)
			img.set_pixel(x, y, col)

	img.save_png(OUT + "mud_drop.png")
	print("  mud_drop.png %dx%d" % [s, s])

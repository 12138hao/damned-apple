extends SceneTree
# 生成 HUD 武器架的金色边框（9-slice 用）。
# 只画边框，中间透明，这样能浮在游戏画面上不挡视野。
# 跑法：godot --headless --path . --script res://tools/gen_hud_frame.gd

const OUT := "res://assets/ui/"


func _initialize() -> void:
	var s := 64
	var img := Image.create_empty(s, s, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	# 距离边缘第几像素 → 颜色。0 是最外圈，8 是最里圈（再往里就透明了）。
	# 从外到内：深棕描边 → 亮金高光 → 金 → 暗金 → 深棕收边，
	# 这样 9-slice 拉伸后仍然有金属的立体感。
	var ramp := [
		Color8(58, 38, 16, 255),     # 0 外描边
		Color8(58, 38, 16, 255),     # 1
		Color8(255, 240, 190, 255),  # 2 高光
		Color8(247, 218, 138, 255),  # 3
		Color8(231, 190, 96, 255),   # 4
		Color8(210, 164, 70, 255),   # 5
		Color8(185, 140, 52, 255),   # 6
		Color8(142, 104, 40, 255),   # 7
		Color8(92, 64, 26, 255),     # 8 内收边
	]

	# 框内填一块半透明深棕：不填的话 9-slice 中间是透明的，
	# 格子之间会露出天空，看着像四个方块浮在蓝色里，不像一个架子。
	var plaque := Color8(38, 26, 15, 236)

	for y in range(s):
		for x in range(s):
			var d: int = min(min(x, y), min(s - 1 - x, s - 1 - y))
			if d < ramp.size():
				img.set_pixel(x, y, ramp[d])
			else:
				img.set_pixel(x, y, plaque)

	img.save_png(OUT + "weapon_rack_frame.png")
	print("  weapon_rack_frame.png %dx%d（9-slice margin 10）" % [s, s])
	quit()

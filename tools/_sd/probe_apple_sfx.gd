extends SceneTree

# 一次撒 21 颗苹果，数落地音效响了几声（原来是 21 声）
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var script = load("res://FallingApple.cs")
	for i in range(21):
		var a = script.new()
		scn.add_child(a)
		a.global_position = Vector2(3800.0 + i * 6.0, 700.0)
		a.WarnTime = 0.0
		a.StartHeight = 60.0 + (i % 7) * 30.0     # 落地时间错开约 0.3 秒
		a.Gravity = 900.0
		a.SfxLand = "collapse"
	print("[AppleSfx] 撒完 21 颗")
	await create_timer(3.0).timeout
	quit()

extends SceneTree

# 看 eden 开头 600px 的藤：左边（贴世界左边界）和右边（羽化收尾）各拍一张

const SHOTS := [
	{"name": "eden_vine_left", "x": 300.0},
	{"name": "eden_vine_right", "x": 560.0},
]

func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.process_mode = Node.PROCESS_MODE_DISABLED
	var cam: Camera2D = box.get_node("Camera2D")
	cam.position_smoothing_enabled = false

	for s in SHOTS:
		box.global_position = Vector2(s["x"], 684.0)
		cam.force_update_scroll()
		for i in range(4):
			await process_frame
		var full := root.get_viewport().get_texture().get_image()
		if full == null:
			print("!! 拿不到视口")
			quit()
			return
		var path := "res://docs/screenshots/%s.png" % s["name"]
		full.save_png(path)
		print("[Shot] ", path, "  ", full.get_size())

	quit()

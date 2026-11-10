extends SceneTree

# eden 巡场：几个位置各拍一张，看树、石头、藤有没有摆歪
const SHOTS := [1200.0, 5000.0, 9000.0, 13000.0, 17000.0, 19400.0]

func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.process_mode = Node.PROCESS_MODE_DISABLED
	var cam: Camera2D = box.get_node("Camera2D")
	cam.position_smoothing_enabled = false

	for x in SHOTS:
		box.global_position = Vector2(x, 684.0)
		cam.force_update_scroll()
		for i in range(4):
			await process_frame
		var full := root.get_viewport().get_texture().get_image()
		var path := "res://docs/screenshots/eden_survey_%d.png" % int(x)
		full.save_png(path)
		print("[Shot] ", path)

	quit()

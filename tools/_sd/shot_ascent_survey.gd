extends SceneTree

# VineAscent 巡场：藤底、爬升中段、弯头、横爬段、传送门
const SHOTS := [
	{"n": "ascent_bottom", "p": Vector2(860, 3800)},
	{"n": "ascent_mid", "p": Vector2(880, 2400)},
	{"n": "ascent_bend", "p": Vector2(1150, 1420)},
	{"n": "ascent_crawl", "p": Vector2(4000, 1400)},
	{"n": "ascent_portal", "p": Vector2(20400, 1400)},
]

func _initialize() -> void:
	var scn: Node2D = load("res://VineAscent.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.process_mode = Node.PROCESS_MODE_DISABLED
	var cam: Camera2D = box.get_node("Camera2D")
	cam.position_smoothing_enabled = false

	for s in SHOTS:
		box.global_position = s["p"]
		cam.force_update_scroll()
		for i in range(4):
			await process_frame
		var full := root.get_viewport().get_texture().get_image()
		var path := "res://docs/screenshots/%s.png" % s["n"]
		full.save_png(path)
		print("[Shot] ", path)

	quit()

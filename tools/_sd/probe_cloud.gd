extends SceneTree

# 在几个位置截图，看云和墙的关系
func _initialize() -> void:
	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var spots := [7700.0, 7900.0, 8200.0, 11900.0, 12100.0, 16000.0]
	var i := 0
	for x in spots:
		box.global_position = Vector2(x, 330)
		box.SnapCamera()
		await create_timer(0.6).timeout
		var img := root.get_viewport().get_texture().get_image()
		if img != null:
			img.save_png("res://docs/screenshots/cloud_%02d_%d.png" % [i, int(x)])
		print("[Cloud] 截图 x=%d" % int(x))
		i += 1
	quit()

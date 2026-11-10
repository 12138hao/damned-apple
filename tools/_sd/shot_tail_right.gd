extends SceneTree

# 右侧甩尾：站到树干右边，确认 FlipH 镜像和右侧预警带都对
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(7050.0, 684.0)
	var boss: Node = scn.get_node("SnakeBossArena/SnakeBoss")

	var t := 0.0
	while t < 45.0:
		await create_timer(0.1).timeout
		t += 0.1
		if boss.State == 4:
			break
	if boss.State != 4:
		print("[Shot] 没等到甩尾")
		quit()
		return

	# 保证站回右侧，不然会被扫到左边去
	box.global_position = Vector2(7100.0, 684.0)
	var marks := [0.6, 1.3, 1.6, 1.72, 1.84, 1.96]
	var last := 0.0
	var idx := 0
	for m in marks:
		await create_timer(m - last).timeout
		last = m
		var img := root.get_viewport().get_texture().get_image()
		if img != null:
			img.save_png("res://docs/screenshots/nr_%02d.png" % idx)
		idx += 1
	print("[Shot] 右侧完成")
	quit()

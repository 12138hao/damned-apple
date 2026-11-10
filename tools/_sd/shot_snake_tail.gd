extends SceneTree

# 专门抓甩尾中段（状态刚变时尾巴长度是 0，必须等半秒）
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	var boss: Node = scn.get_node_or_null("SnakeBossArena/SnakeBoss")
	if boss == null:
		quit()
		return

	await create_timer(8.5).timeout
	var got := 0
	var t := 0.0
	while t < 30.0 and got < 3:
		await create_timer(0.12).timeout
		t += 0.12
		if boss.State == 4:
			await create_timer(0.72).timeout
			got += 1
			var img := root.get_viewport().get_texture().get_image()
			if img != null:
				var path := "res://docs/screenshots/tail_%d.png" % got
				img.save_png(path)
				print("[Shot] 尾巴 ", got, " -> ", path)
			# 等这次甩尾结束
			await create_timer(1.6).timeout
			t += 1.6
	quit()

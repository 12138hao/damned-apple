extends SceneTree

# 甩尾连拍：进 Tail 状态后每 0.12s 抓一张，必定有一张是最大幅度
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
	var done := 0
	var t := 0.0
	while t < 30.0 and done < 2:
		await create_timer(0.1).timeout
		t += 0.1
		if boss.State != 4:
			continue
		done += 1
		for i in range(6):
			await create_timer(0.12).timeout
			t += 0.12
			var img := root.get_viewport().get_texture().get_image()
			if img != null:
				img.save_png("res://docs/screenshots/tw_%d_%d.png" % [done, i])
				print("[Shot] tw_%d_%d" % [done, i])
		await create_timer(1.2).timeout
		t += 1.2
	quit()

extends SceneTree

# 新版甩尾连拍：等到 Tail 状态，按固定节拍抓 12 张，覆盖「蓄力 → 抽出 → 收尾 → 三波苹果」
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var boss: Node = scn.get_node_or_null("SnakeBossArena/SnakeBoss")
	# 竞技场实例在 eden.tscn 里被挪过位置，别写死坐标，按 Boss 的实际位置进区
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	if boss == null:
		print("[Shot] 没找到 boss")
		quit()
		return

	var t := 0.0
	while t < 45.0:
		await create_timer(0.1).timeout
		t += 0.1
		if boss.State != 4:
			continue

		print("[Shot] 抓到甩尾，开始连拍")
		var marks := [0.35, 0.75, 1.15, 1.45, 1.58, 1.68, 1.78, 1.88, 2.0, 2.15, 2.3, 3.2, 4.2, 5.2]
		var last := 0.0
		var idx := 0
		for m in marks:
			await create_timer(m - last).timeout
			last = m
			var img := root.get_viewport().get_texture().get_image()
			if img != null:
				img.save_png("res://docs/screenshots/nt_%02d.png" % idx)
				print("[Shot] nt_%02d  t=%.2f" % [idx, m])
			idx += 1
		break

	quit()

extends SceneTree

# 复现：从第二章井里上来进第三章，看玩家会不会卡住
func _initialize() -> void:
	var gs: Node = root.get_node_or_null("GameState")
	gs.MarkWellEntry()

	var scn: Node2D = load("res://jungle.tscn").instantiate()
	root.add_child(scn)
	for i in range(6):
		await process_frame

	var box: Node2D = scn.get_node_or_null("box")
	var well: Node = scn.get_node_or_null("Well")
	if box == null or well == null:
		print("[Well] 节点缺失 box=%s well=%s" % [str(box), str(well)])
		quit()
		return

	print("[Well] 井=%s 玩家初始=%s" % [str(well.global_position), str(box.global_position)])
	for i in range(14):
		await create_timer(0.15).timeout
		print("[Well] t=%.2f pos=%s 过场=%s vel=%s" % [
			i * 0.15, str(box.global_position), str(box.InCutscene), str(box.velocity)])
		if i == 3:
			print("[Well] 内部: %s" % str(well.call("DebugWell")))

	# 交还操作后试着往右走 1 秒，看能不能动
	print("[Well] 走之前 pos=%s 过场=%s" % [str(box.global_position), str(box.InCutscene)])
	Input.action_press("move_right")
	await create_timer(1.0).timeout
	Input.action_release("move_right")
	await create_timer(0.3).timeout
	print("[Well] 往右走 1 秒后 pos=%s" % str(box.global_position))
	var img := root.get_viewport().get_texture().get_image()
	if img != null: img.save_png("res://docs/screenshots/well_after.png")
	quit()

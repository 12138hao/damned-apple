extends SceneTree

# 检查两边空气墙：把玩家挪到左墙/右墙附近，截图看「靠近才显示」的墙有没有出现，
# 并试着继续往外走，看会不会被挡住。
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	box.global_position = Vector2(6900.0, 684.0)
	var arena: Node = scn.get_node_or_null("SnakeBossArena")
	if arena == null:
		quit()
		return

	var boss: Node = arena.get_node_or_null("SnakeBoss")
	# 等开战（对话可能还要几秒）：State 2 = Idle（0=Dormant 1=Intro）
	var t := 0.0
	while t < 25.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss != null and boss.State == 2:
			break
	print("[Probe] 开战完成，用时 %.1fs" % t)
	await create_timer(0.6).timeout

	# 左墙
	box.global_position = Vector2(6560.0, 684.0)
	await create_timer(0.5).timeout
	var img := root.get_viewport().get_texture().get_image()
	if img != null:
		img.save_png("res://docs/screenshots/nw_left.png")
	print("[Probe] 左墙截图")

	# 往左硬推 1.2 秒
	for i in range(1):
		Input.action_press("move_left")
	await create_timer(1.2).timeout
	Input.action_release("move_left")
	print("[Probe] 推完之后玩家 x = %.1f" % box.global_position.x)

	# 右墙
	box.global_position = Vector2(7380.0, 684.0)
	await create_timer(0.5).timeout
	img = root.get_viewport().get_texture().get_image()
	if img != null:
		img.save_png("res://docs/screenshots/nw_right.png")
	print("[Probe] 右墙截图")

	Input.action_press("move_right")
	await create_timer(1.2).timeout
	Input.action_release("move_right")
	print("[Probe] 推完之后玩家 x = %.1f" % box.global_position.x)
	quit()

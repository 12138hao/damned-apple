extends SceneTree

# 量一下甩击（从第 5 帧出手，到尾巴收回消失）到底用了多久
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var boss: Node = scn.get_node("SnakeBossArena/SnakeBoss")
	var tail: Sprite2D = boss.get_node_or_null("TailArt")
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)

	var t := 0.0
	while t < 40.0:
		await create_timer(0.1).timeout
		t += 0.1
		if boss.State == 4:
			break

	# 按物理帧采样：出手帧 >= 5 开始计时，尾巴隐藏结束
	var swingT := -1.0
	var doneT := -1.0
	var clock := 0.0
	for i in range(300):
		await physics_frame
		clock += 1.0 / 60.0
		if tail == null:
			break
		if swingT < 0.0 and tail.visible and tail.frame >= 5:
			swingT = clock
		if swingT >= 0.0 and not tail.visible:
			doneT = clock
			break

	if swingT < 0.0 or doneT < 0.0:
		print("[Timing] 没捕捉到完整甩击 swing=%.2f done=%.2f" % [swingT, doneT])
	else:
		print("[Timing] 甩击总时长 = %.3f 秒（出手帧5 → 尾巴消失）" % (doneT - swingT))
	quit()

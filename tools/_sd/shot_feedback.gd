extends SceneTree

# 验证受击反馈：Boss 血条数字/残影会不会动，小怪有没有血条和闪红
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")

	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(1.0).timeout

	# 拿木棒
	var bat = load("res://assets/weapons/bat.tres")
	var slots = box.Weapons
	slots[0] = bat
	box.Weapons = slots
	Input.action_press("slot1")
	await create_timer(0.2).timeout
	Input.action_release("slot1")
	await create_timer(0.2).timeout

	var sp: Vector2 = box.get_global_transform_with_canvas().origin
	var mm := InputEventMouseMotion.new()
	mm.position = sp + Vector2(140, 0)
	Input.parse_input_event(mm)
	await create_timer(0.2).timeout

	var img0 := root.get_viewport().get_texture().get_image()
	if img0 != null: img0.save_png("res://docs/screenshots/fb_boss_before.png")

	Input.action_press("attack")
	await create_timer(0.55).timeout
	Input.action_release("attack")
	await create_timer(0.3).timeout
	var img1 := root.get_viewport().get_texture().get_image()
	if img1 != null: img1.save_png("res://docs/screenshots/fb_boss_after.png")
	print("[FB] boss hp=%d" % boss.Hp)

	# 小怪：放在右边一点，冻住
	var scene: PackedScene = load("res://SnakeMinion.tscn")
	var m: Node2D = scene.instantiate()
	arena.add_child(m)
	m.global_position = box.global_position + Vector2(55, 0)
	m.set_physics_process(false)
	await create_timer(0.9).timeout
	print("[FB] 小怪 pos=%s scale=%s mod=%s" % [str(m.global_position), str(m.scale), str(m.modulate)])

	Input.action_press("attack")
	await create_timer(0.55).timeout
	Input.action_release("attack")
	await create_timer(0.10).timeout
	var img2 := root.get_viewport().get_texture().get_image()
	if img2 != null: img2.save_png("res://docs/screenshots/fb_minion_hit.png")
	await create_timer(0.5).timeout
	print("[FB] 小怪 hp=%d/%d  bar=%s" % [m.Hp, m.MaxHp, str(m.get_node_or_null("HpBar"))])
	var img3 := root.get_viewport().get_texture().get_image()
	if img3 != null: img3.save_png("res://docs/screenshots/fb_minion_after.png")
	quit()

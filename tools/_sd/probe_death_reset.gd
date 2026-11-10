extends SceneTree

# 验证：玩家死在 Boss 战里 → 战斗必须立刻收掉；复活后再进场能重新开战
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(10):
		await process_frame

	var box: Node2D = scn.get_node("box")
	var arena: Node = scn.get_node("SnakeBossArena")
	var boss: Node = arena.get_node("SnakeBoss")
	var cam: Camera2D = box.get_node("Camera2D")

	# 进场开战
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	var t := 0.0
	while t < 30.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	await create_timer(0.8).timeout
	print("[Dead] 开战后 state=%d zoom=%s 墙=%d bar=%s" % [
		boss.State, str(cam.zoom), CountWalls(arena), str(FindBar(arena))])

	# 手动塞一只小怪和一团毒，验证重置时会不会清掉
	var mm: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(mm)
	mm.global_position = box.global_position + Vector2(-80, 0)
	mm.HopForward = 0.0
	mm.HopUp = 0.0
	mm.HopInterval = 99.0
	await create_timer(0.4).timeout
	print("[Dead] 塞了小怪，arena 子节点=%d" % arena.get_child_count())

	# 弄死玩家
	box.TakeDamage(9999, box.global_position + Vector2(10, 0))
	await create_timer(0.4).timeout
	print("[Dead] 玩家死后 alive=%s zoom=%s 墙=%d bar=%s" % [
		str(box.IsAlive), str(cam.zoom), CountWalls(arena), str(FindBar(arena))])
	print("[Dead] boss state=%d hp=%d/%d invuln=%s 场上小怪=%d" % [
		boss.State, boss.Hp, boss.MaxHp, str(boss.Invulnerable), CountMinions(arena)])

	# 等复活
	t = 0.0
	while t < 10.0:
		await create_timer(0.2).timeout
		t += 0.2
		if box.IsAlive:
			break
	print("[Dead] 复活完成 用时 %.1fs  pos=%s" % [t, str(box.global_position)])

	# 走出去再走进来
	box.global_position = Vector2(boss.global_position.x - 400.0, 684.0)
	await create_timer(0.5).timeout
	box.global_position = Vector2(boss.global_position.x - 60.0, 684.0)
	t = 0.0
	while t < 10.0:
		await create_timer(0.2).timeout
		t += 0.2
		if boss.State == 2:
			break
	print("[Dead] 再进场 state=%d 墙=%d bar=%s" % [boss.State, CountWalls(arena), str(FindBar(arena))])
	quit()

func CountWalls(arena: Node) -> int:
	var n := 0
	for c in arena.get_children():
		if c.get_node_or_null("Visual") != null:
			n += 1
	return n

func FindBar(arena: Node) -> String:
	for c in arena.get_children():
		if c is CanvasLayer:
			return c.get_script().resource_path if c.get_script() != null else "CanvasLayer"
	return "无"

func CountMinions(arena: Node) -> int:
	var n := 0
	for c in arena.get_children():
		var s = c.get_script()
		if s != null and str(s.resource_path).ends_with("SnakeMinion.cs"):
			n += 1
	return n

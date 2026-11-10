extends SceneTree

# 只验证小怪受击闪红：直接调 TryTakeHit，然后读 Art.modulate
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var arena: Node = scn.get_node("SnakeBossArena")
	var m: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	m.global_position = Vector2(3800.0, 700.0)
	m.HopForward = 0.0
	m.HopUp = 0.0
	m.HopInterval = 99.0
	await create_timer(0.6).timeout

	var art := m.get_node("Art")
	print("[Flash] 打之前 modulate=%s  hp=%d" % [str(art.modulate), m.Hp])
	m.TryTakeHit(30, m.global_position + Vector2(-10, 0), 0.0, 0.0, true)
	await create_timer(0.05).timeout
	print("[Flash] 打之后 modulate=%s  hp=%d" % [str(art.modulate), m.Hp])
	await create_timer(0.35).timeout
	print("[Flash] 0.4s 后 modulate=%s  hp=%d" % [str(art.modulate), m.Hp])
	quit()

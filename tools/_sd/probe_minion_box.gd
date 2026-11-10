extends SceneTree

# 看小怪跳跃时判定框有没有跟着画面走，以及世界尺寸是否等于贴图
func _initialize() -> void:
	var scn: Node2D = load("res://eden.tscn").instantiate()
	root.add_child(scn)
	for i in range(8):
		await process_frame

	var arena: Node = scn.get_node("SnakeBossArena")
	var m: Node2D = load("res://SnakeMinion.tscn").instantiate()
	arena.add_child(m)
	m.global_position = Vector2(3700.0, 690.0)
	await create_timer(0.6).timeout

	var cs: CollisionShape2D = m.get_node("CollisionShape2D")
	var art: Sprite2D = m.get_node("Art")
	var last := ""
	for i in range(30):
		await create_timer(0.08).timeout
		var box = cs.shape
		var s := Vector2(box.size)
		var line := "%-6s f%d  shape=%s pos=%s 世界=%.1fx%.1f 底边=%.1f" % [
			art.texture.resource_path.get_file().get_basename(),
			art.frame, str(s), str(cs.position),
			s.x * m.scale.x, s.y * m.scale.y,
			cs.position.y + s.y * 0.5]
		if line != last:
			print("[Box] " + line)
			last = line
	quit()

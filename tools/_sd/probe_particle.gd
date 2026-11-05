extends SceneTree

# 直接检查 MudZone 下粒子节点的实际状态，确认飞溅到底有没有发射。

func _initialize() -> void:
	var scene: PackedScene = load("res://jungle.tscn")
	var node := scene.instantiate()
	root.add_child(node)
	await process_frame

	var mz := node.get_node_or_null("MudZone")
	print("[Probe] MudZone = ", mz)
	if mz == null: quit(); return

	print("--- MudZone 子节点 ---")
	for c in mz.get_children():
		var line := "  %s (%s)" % [c.name, c.get_class()]
		if c is CPUParticles2D:
			line += "  amount=%d  emitting=%s  one_shot=%s  pos=%s  texture=%s" % [
				c.amount, c.emitting, c.one_shot, c.position,
				(c.texture.resource_path if c.texture else "NULL")]
			line += "  scale_amount=[%.2f, %.2f]" % [c.scale_amount_min, c.scale_amount_max]
		print(line)

	var box: CharacterBody2D = node.get_node_or_null("box")
	box.global_position = Vector2(1500, 330)
	for i in range(90):
		await physics_frame
	print("[Probe] 站位 y=%.1f" % box.global_position.y)

	# 跳
	Input.action_press("jump")
	for i in range(7):
		await physics_frame
	Input.action_release("jump")

	print("--- 跳跃后 ---")
	for c in mz.get_children():
		if c is CPUParticles2D:
			print("  %s emitting=%s  活跃粒子=%d" % [c.name, c.emitting, c.get("particle_count") if c.has_method("get") else -1])

	# 打印玩家和粒子世界位置
	var splash := mz.get_node_or_null("SplashHard")
	if splash != null:
		print("[Probe] SplashHard 世界位置 = ", splash.global_position)
	print("[Probe] 玩家世界位置 = ", box.global_position)

	quit()

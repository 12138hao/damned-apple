extends SceneTree

# 在各种情况下跳一跳，确认统一的跳跃音效不会炸（音效文件还没有，应该是静默）。
# 顺便把 AudioManager 里那三个固定值的实际生效结果打出来。

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	box.global_position = Vector2(1600, 100)
	for i in range(300):
		await physics_frame
		if box.is_on_floor():
			break
	for i in range(6):
		await physics_frame

	print("jump 音效文件存在 = ", AudioManagerHasSfx())

	for round in range(3):
		Input.action_press("jump")
		for i in range(2):
			await physics_frame
		Input.action_release("jump")
		for i in range(3):
			await physics_frame
		print("第%d次跳  y=%.1f  vy=%.1f" % [round + 1, box.global_position.y, box.velocity.y])
		for i in range(40):
			await physics_frame
			if box.is_on_floor():
				break

	# 进泥潭再跳一次（走的是同一条音效路径，但现在不再调音量音高）
	print("泥潭里跳：")
	var zone = node.get_node_or_null("MudZone")
	if zone != null:
		box.call("EnterMud", zone)
		for i in range(4):
			await physics_frame
		Input.action_press("jump")
		for i in range(2):
			await physics_frame
		Input.action_release("jump")
		for i in range(3):
			await physics_frame
		print("  泥潭跳 y=%.1f vy=%.1f  InMud=%s"
			% [box.global_position.y, box.velocity.y, box.call("get", "InMud")])
	else:
		print("  没找到 MudZone")
	quit()

func AudioManagerHasSfx() -> bool:
	var am = root.get_node_or_null("AudioManager")
	if am == null:
		return false
	return am.call("HasSfx", "jump")

extends SceneTree

# 复现老大说的场景：站在拳头够得着、但身体碰不到的距离上一直出拳，
# 看他会不会莫名其妙掉血。海盗苹果全程不还手（DetectRange=0）。

var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_enemy = node.get_node("pirate_apple1")
	_enemy.set("DetectRange", 0.0)
	_box = node.get_node("box")

	for c in node.get_children():
		if c is CharacterBody2D and c != _enemy and c != _box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED

	_box.global_position = Vector2(_enemy.global_position.x - 220, _enemy.global_position.y - 60)
	for i in range(200):
		await physics_frame
		if _box.is_on_floor():
			break
	var ground_y: float = _box.global_position.y
	var ex: float = _enemy.global_position.x
	print("怪 x=%.1f  地面 y=%.1f" % [ex, ground_y])
	print("每格：站在这个距离上连拍 2 秒拳头，全程不动，记录掉血")
	print("")

	for d in [38, 42, 46, 50, 55, 60, 70]:
		_box.call("HealFull")
		_box.global_position = Vector2(ex - d, ground_y)
		_box.velocity = Vector2.ZERO
		# 怪在右边，先把玩家转向右，否则拳头打在空气里
		Input.action_press("move_right")
		for i in range(2):
			await physics_frame
		Input.action_release("move_right")
		_box.global_position = Vector2(ex - d, ground_y)
		_box.velocity = Vector2.ZERO
		for i in range(10):
			await physics_frame
		var hp0: int = int(_box.call("get", "Hp"))
		var enemy_hp0: int = int(_enemy.get("Hp"))
		for round in range(12):     # 12 次出拳
			Input.action_press("attack")
			for i in range(4):
				_enemy.global_position = Vector2(ex, _enemy.global_position.y)
				await physics_frame
			Input.action_release("attack")
			for i in range(12):
				_enemy.global_position = Vector2(ex, _enemy.global_position.y)
				await physics_frame
		var hp: int = int(_box.call("get", "Hp"))
		var enemy_hp: int = int(_enemy.get("Hp"))
		print("  相距 %2d  →  自己掉血 %2d   怪掉血 %3d   %s"
			% [d, hp0 - hp, enemy_hp0 - enemy_hp,
			   "★自己白挨打" if hp0 - hp > 0 else ""])
	quit()

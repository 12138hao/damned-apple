extends SceneTree

# 量海盗苹果劈砍「真正能砍到多远」。
# 做法：把接触伤害关掉（只看刀）、把突进关掉（原地挥），一格一格试距离。

var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _state() -> String:
	return str(_enemy.call("DebugPirate")).split("state=")[1].split(" ")[0]

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_enemy = node.get_node("pirate_apple1")
	_enemy.set("Damage", 0)          # 关掉接触伤害，只量刀
	_enemy.set("DashDistance", 0.0)  # 关掉突进，原地挥
	_box = node.get_node("box")

	for c in node.get_children():
		if c is CharacterBody2D and c != _enemy and c != _box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED

	_box.global_position = Vector2(_enemy.global_position.x - 200, _enemy.global_position.y - 60)
	for i in range(200):
		await physics_frame
		if _box.is_on_floor():
			break
	var ground_y: float = _box.global_position.y
	var ex: float = _enemy.global_position.x
	print("怪 x=%.1f  地面 y=%.1f" % [ex, ground_y])
	print("每次命中掉 30（接触伤害已关）")
	print("")

	for d in [30, 50, 70, 90, 100, 110, 120, 130, 140, 150, 170]:
		# 等它回到巡逻，保证每次都是完整一套
		for i in range(600):
			if _state() == "Patrol":
				break
			await physics_frame
		_box.call("HealFull")
		_box.global_position = Vector2(ex - d, ground_y)
		_box.velocity = Vector2.ZERO
		var hp0: int = int(_box.call("get", "Hp"))
		var best := 0
		for i in range(240):
			_enemy.global_position = Vector2(ex, _enemy.global_position.y)
			await physics_frame
			_enemy.global_position = Vector2(ex, _enemy.global_position.y)
			var drop: int = hp0 - int(_box.call("get", "Hp"))
			best = max(best, drop)
			if best >= 30:
				break
		print("  相距 %3d  →  掉血合计 %2d   %s" % [d, best, "★砍到" if best >= 30 else ""])
	quit()

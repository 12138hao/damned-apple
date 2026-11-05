extends SceneTree

# 量「身体接触伤害」的作用距离：从贴着苹果到刀尖那么远，一格一格试，
# 看哪一段会掉血。目标：只贴着苹果本体才掉，站在刀旁边不掉。

var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_enemy = node.get_node("pirate_apple1")
	_enemy.set("DetectRange", 0.0)          # 别让它出招，只测接触
	_box = node.get_node("box")

	for c in node.get_children():
		if c is CharacterBody2D and c != _enemy and c != _box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED

	# 先把玩家放到地面站稳，记录地面 y
	_box.global_position = Vector2(_enemy.global_position.x - 200, _enemy.global_position.y - 60)
	for i in range(200):
		await physics_frame
		if _box.is_on_floor():
			break
	var ground_y: float = _box.global_position.y
	var ex: float = _enemy.global_position.x
	print("怪 x=%.1f  地面 y=%.1f  玩家半宽=16" % [ex, ground_y])
	print("接触伤害会掉 10 血，一格测 70 帧（够无敌转完一轮）")
	print("")

	var dists := [40, 46, 50, 55, 60, 65, 70, 80, 90, 100, 120, 140]
	for d in dists:
		_box.call("HealFull")
		_box.global_position = Vector2(ex - d, ground_y)
		_box.velocity = Vector2.ZERO
		var hp0: int = int(_box.call("get", "Hp"))
		for i in range(70):
			# 怪还在自己巡逻，位置一直在变 → 每帧钉回原处，只留判定区
			_enemy.global_position = Vector2(ex, _enemy.global_position.y)
			await physics_frame
			_enemy.global_position = Vector2(ex, _enemy.global_position.y)
		var hp: int = int(_box.call("get", "Hp"))
		print("  起始相距 %3d  →  掉血 %2d   测完相距 %.1f   %s"
			% [d, hp0 - hp, abs(_box.global_position.x - _enemy.global_position.x),
			   "★中招" if hp0 - hp > 0 else ""])

	# 再确认扫刀照样能打到（不会因为缩小接触框把刀伤一起砍掉）
	print("")
	print("刀伤回归测试：")
	_enemy.set("DetectRange", 460.0)
	_box.call("HealFull")
	_box.global_position = Vector2(ex - 120, ground_y)
	for i in range(20):
		await physics_frame
	var last: int = int(_box.call("get", "Hp"))
	var total := 0
	for i in range(600):
		await physics_frame
		var hp: int = int(_box.call("get", "Hp"))
		if hp != last:
			print("  掉血 %d" % [last - hp])
			total += last - hp
			last = hp
			if total >= 60:
				break
	print("  合计 %d" % total)
	quit()

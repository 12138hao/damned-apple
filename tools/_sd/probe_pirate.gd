extends SceneTree

# 海盗苹果验收：连招流程、每段 30 伤害、突进 50 像素、命中距离 100

var _box: CharacterBody2D
var _enemy: CharacterBody2D
var _last := ""
var _hp0: int = 0
var _dash_from := 0.0

func _dbg() -> String:
	return str(_enemy.call("DebugPirate"))

func _state() -> String:
	var s := _dbg()
	return s.split("state=")[1].split(" ")[0]

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_box = node.get_node("box")
	# 找一块平地：从 (1200, 100) 掉下去
	_box.global_position = Vector2(1600, 100)
	for i in range(300):
		await physics_frame
		if _box.is_on_floor():
			break
	await _settle(10)
	var ground: Vector2 = _box.global_position
	print("落地点 = ", ground.snapped(Vector2(0.1, 0.1)), "  在地面=", _box.is_on_floor())

	_enemy = load("res://PirateApple.tscn").instantiate()
	node.add_child(_enemy)
	_enemy.global_position = ground + Vector2(150, -20)
	await _settle(40)
	print("怪落定 = ", _enemy.global_position.snapped(Vector2(0.1, 0.1)))

	_hp0 = _box.get("Hp")
	print("玩家初始 HP = ", _hp0)
	print("=== 开始观察 ===")

	var t0 := Time.get_ticks_msec()
	var last_hp := _hp0
	while Time.get_ticks_msec() - t0 < 7000:
		await physics_frame
		var s := _dbg()
		var st := _state()
		if st != _last:
			if st == "Dash":
				_dash_from = _enemy.global_position.x
			if _last == "Dash":
				var d: float = abs(_enemy.global_position.x - _dash_from)
				print("  [突进] 位移 %.1f 像素（目标 50）" % d)
			print("  %s  怪x=%.1f  玩家x=%.1f  距离=%.1f  HP=%d" % [
				st, _enemy.global_position.x, _box.global_position.x,
				abs(_box.global_position.x - _enemy.global_position.x), _box.get("Hp")])
			_last = st
		var hp: int = _box.get("Hp")
		if hp != last_hp:
			print("    ★ 掉血 %d → %d（-%d）  此时 距离=%.1f  状态=%s  stage=%s" % [
				last_hp, hp, last_hp - hp,
				abs(_box.global_position.x - _enemy.global_position.x), st,
				s.split("stage=")[1].split(" ")[0]])
			last_hp = hp
		if hp <= 0:
			print("  玩家阵亡，停止观察")
			break
	print("=== 结束 ===")
	print("最终 HP = ", _box.get("Hp"), "  （初始 %d）" % _hp0)
	quit()

func _settle(n: int) -> void:
	for i in range(n):
		await physics_frame

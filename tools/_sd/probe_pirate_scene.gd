extends SceneTree

# 验证 jungle.tscn 里摆好的 pirate_apple1：落地、能发现玩家、两段各 30 伤害、突进距离。

var _box: CharacterBody2D
var _enemy: CharacterBody2D
var _hp0 := 0

func _dbg() -> Dictionary:
	var s := str(_enemy.call("DebugPirate"))
	var d := {}
	for part in s.split(" "):
		var kv := part.split("=")
		if kv.size() == 2:
			d[kv[0]] = kv[1]
	return d

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_enemy = node.get_node("pirate_apple1")
	_box = node.get_node("box")
	# 场景里还有别的怪，会把玩家抓走，先全禁掉
	for c in node.get_children():
		if c is CharacterBody2D and c != _enemy and c != _box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED
	print("找到 pirate_apple1  position=", _enemy.global_position)

	# 让它先自己落地站稳
	for i in range(90):
		await physics_frame
	print("90 帧后 position=", _enemy.global_position, "  onFloor=", _enemy.is_on_floor())

	_box.global_position = Vector2(_enemy.global_position.x - 90, _enemy.global_position.y - 120)
	for i in range(120):
		await physics_frame
		if _box.is_on_floor():
			break
	for i in range(6):
		await physics_frame
	print("玩家落位 ", _box.global_position, "  HP=", _box.call("get", "Hp"))

	# 等一套连招彻底打完再开始记，否则会从半套连招的中间开始数
	for i in range(900):
		await physics_frame
		var st0: String = _dbg().get("state", "")
		if st0 == "Patrol":
			break
	# 站定后把血回满，从干净状态开始
	_box.call("HealFull")
	await physics_frame
	print("开始监控，玩家 HP=", _box.call("get", "Hp"), "  怪 x=%.1f" % _enemy.global_position.x)

	var start_x: float = _enemy.global_position.x
	var last_hp: int = int(_box.call("get", "Hp"))
	var hits: Array = []
	var dash_from := 0.0
	var dash_to := 0.0
	var prev_state := ""

	for i in range(600):
		await physics_frame
		var d := _dbg()
		var st: String = d.get("state", "")
		if st == "Dash" and prev_state != "Dash":
			dash_from = _enemy.global_position.x
		if prev_state == "Dash" and st != "Dash":
			dash_to = _enemy.global_position.x
			print("  突进位移 %.1f" % abs(dash_to - dash_from))
		if st == "Strike" and prev_state != "Strike":
			print("  → Strike stage=%s 横向距离=%.0f 纵向差=%.0f 玩家y=%.0f 怪y=%.0f"
				% [d.get("stage", "?"),
				   abs(_box.global_position.x - _enemy.global_position.x),
				   _box.global_position.y - _enemy.global_position.y,
				   _box.global_position.y, _enemy.global_position.y])
		# Strike 期间每帧盯一次血，抓命中时刻
		var hp2 := int(_box.call("get", "Hp"))
		if hp2 != last_hp:
			hits.append(last_hp - hp2)
			print("  掉血 %d  (state=%s stage=%s 距离=%.0f)"
				% [last_hp - hp2, st, d.get("stage", "?"), abs(_box.global_position.x - _enemy.global_position.x)])
			last_hp = hp2
		prev_state = st
		if hits.size() >= 3:
			break

	print("总掉血 = ", hits, "   合计 ", hits.reduce(func(a, b): return a + b, 0))
	print("最终：玩家 HP=", _box.call("get", "Hp"), "  怪 x=%.1f（起始 %.1f）" % [_enemy.global_position.x, start_x])
	quit()

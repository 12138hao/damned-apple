extends SceneTree

# 验证「贴着 pirate_apple 不掉血但会红闪 + 响受击音」这个 bug 修没修。
# 判据：贴着站 3 秒，血量不掉、也不被击退（击退 = 受击反馈真的触发了）。
# 再顺手确认刀的 30 点伤害仍然打得出来。

var _box: CharacterBody2D
var _enemy: CharacterBody2D

func _dbg() -> String:
	return str(_enemy.call("DebugPirate"))

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	_enemy = node.get_node("pirate_apple1")
	_enemy.set("DetectRange", 0.0)     # 先按住，单独测「接触」
	_box = node.get_node("box")

	# 场景里还有别的怪（undead_apple2 会隔着两百像素伸手把玩家抓走），测接触之前先全禁掉
	for c in node.get_children():
		if c is CharacterBody2D and c != _enemy and c != _box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED
			c.visible = false
			print("  已禁用 ", c.name, " @ ", (c as Node2D).global_position)

	# 直接把玩家摆到怪身上（横向 20，身体判定框一定会重叠）
	print("  怪 pos=", _enemy.global_position, "  玩家原 pos=", _box.global_position)
	_box.global_position = Vector2(_enemy.global_position.x - 20, _enemy.global_position.y - 60)
	print("  瞬移后 玩家 pos=", _box.global_position)
	for i in range(200):
		await physics_frame
		if _box.is_on_floor():
			break
	for i in range(6):
		await physics_frame
	print("  落地后 玩家 pos=", _box.global_position, "  HP=", _box.call("get", "Hp"))

	var hp0: int = int(_box.call("get", "Hp"))
	var max_vx := 0.0
	var min_y := 99999.0
	var last_hp := hp0
	print("  敌人 Damage 属性 = ", _enemy.get("Damage"))
	for i in range(180):        # 3 秒
		await physics_frame
		max_vx = max(max_vx, abs(_box.velocity.x))
		min_y = min(min_y, _box.global_position.y)
		var now_hp: int = int(_box.call("get", "Hp"))
		if now_hp != last_hp:
			print("  第%d帧掉血 %d  玩家pos=%s  敌人state=%s"
				% [i, last_hp - now_hp, _box.global_position, _dbg()])
			last_hp = now_hp
	print("【接触测试】3 秒贴身")
	print("  血量 %d -> %d   横向最大速度 %.1f   最高抬升 %.1f"
		% [hp0, _box.call("get", "Hp"), max_vx, _box.global_position.y - min_y])
	print("  期望：血量不变、横向速度 ≈ 0（没有被击退）")

	# 然后放开 Detection，让它真的砍一刀，确认 30 点伤害没被误伤
	print("【刀伤测试】")
	_enemy.set("DetectRange", 460.0)
	_box.global_position = Vector2(_enemy.global_position.x - 90, _enemy.global_position.y - 60)
	for i in range(20):
		await physics_frame
	var last: int = int(_box.call("get", "Hp"))
	var total := 0
	for i in range(600):
		await physics_frame
		var hp: int = int(_box.call("get", "Hp"))
		if hp != last:
			print("  掉血 %d  (state=%s)" % [last - hp, _dbg()])
			total += last - hp
			last = hp
			if total >= 60:
				break
	print("  合计 %d" % total)
	quit()

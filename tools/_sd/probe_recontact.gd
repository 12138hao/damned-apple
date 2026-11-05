extends SceneTree

# 精确测「重新接触清无敌」这一条：
#   0) 贴着站，等它打第一下
#   1) 往后退到 DIST（会不会超过 LeaveDistance 就是这里决定的）
#   2) 走回贴身位
#   3) 数「回位之后过多久才挨第二下」
# 立刻挨 = 无敌被清掉了；等 ~60 帧（1 秒）= 无敌正常生效。
#
# 用法：-- --leave=70

const DIST := 110.0     # 退开多远（比 70 大、比 120 小，正好卡在中间）
const TOUCH := 42.0

func _initialize() -> void:
	var leave := -1.0
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--leave="): leave = float(a.substr(8))

	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	await physics_frame

	var box: CharacterBody2D = node.get_node("box")
	var enemy: CharacterBody2D = node.get_node("pirate_apple1")
	enemy.set("DetectRange", 0.0)        # 不出招，只留接触伤害
	if leave > 0.0:
		enemy.set("LeaveDistance", leave)
	else:
		leave = float(enemy.get("LeaveDistance"))   # 不传就用场景里的值

	for c in node.get_children():
		if c is CharacterBody2D and c != enemy and c != box:
			c.set("Damage", 0)
			c.process_mode = Node.PROCESS_MODE_DISABLED

	var ex: float = enemy.global_position.x
	var ey: float = enemy.global_position.y
	box.global_position = Vector2(ex - TOUCH, ey - 80)
	for i in range(200):
		await physics_frame
		if box.is_on_floor():
			break
	var gy: float = box.global_position.y
	for i in range(20):
		await physics_frame

	# 0) 等第一下
	var started := false
	for i in range(300):
		box.global_position = Vector2(ex - TOUCH, gy)
		await physics_frame
		if int(box.call("get", "Hp")) < 100:
			started = true
			break
	if not started:
		print("!! 第一下都没打到，测试无效"); quit(); return

	# 1) 退开 0.35 秒
	for i in range(21):
		enemy.global_position = Vector2(ex, enemy.global_position.y)
		box.global_position = Vector2(ex - DIST, gy)
		await physics_frame

	# 2) 回位，数多久挨第二下
	var wait := -1
	var hp_now: int = int(box.call("get", "Hp"))
	for i in range(300):
		enemy.global_position = Vector2(ex, enemy.global_position.y)
		box.global_position = Vector2(ex - TOUCH, gy)
		await physics_frame
		if int(box.call("get", "Hp")) < hp_now:
			wait = i
			break

	var verdict := "立刻挨 → 无敌被清掉了 ✗"
	if wait < 0:
		verdict = "一直没挨到"
	elif wait >= 30:
		verdict = "等满了 ≳1 秒 → 无敌正常生效 ✓"
	print("LeaveDistance=%-6.0f  退到 %.0f 再回来，第二下等了 %d 帧  %s"
		% [leave, DIST, wait, verdict])
	quit()

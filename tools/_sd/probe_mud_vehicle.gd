extends SceneTree

# 验「载具进泥潭移动极其缓慢」
# 泥潭状态直接用 EnterMud/ExitMud 强制切换（这就是 MudPit 内部调的那两个方法），
# 免得靠 Area2D 进出事件 —— headless 下瞬移不一定会立刻派发 enter/exit，测出来全是脏数据。

var _box: CharacterBody2D
var _zones: Array = []

func _settle(frames: int) -> void:
	for i in range(frames):
		await physics_frame

func _speed(frames: int = 20) -> float:
	Input.action_release("move_left")
	Input.action_release("move_right")
	await _settle(8)
	var x0: float = _box.global_position.x
	Input.action_press("move_right")
	await _settle(12)                 # 先跑到稳态，别把起步加速算进去
	var xs: float = _box.global_position.x
	var vmax := 0.0
	for i in range(frames):
		await physics_frame
		vmax = max(vmax, abs(_box.velocity.x))   # 峰值 = 稳态速度，撞墙反弹也不会被拉低
	var dx: float = _box.global_position.x - xs
	Input.action_release("move_right")
	print("    稳态速度 %.1f 像素/秒（含起步的平均 %.1f）" % [vmax, (dx + (xs - x0)) / ((frames + 12) / 60.0)])
	return vmax

func _set_mud(on: bool) -> void:
	for z in _zones:
		if on:
			_box.call("EnterMud", z)
		else:
			_box.call("ExitMud", z)

func _ride(on: bool) -> bool:
	# 按键要跨过一整个物理帧才会被 IsActionJustPressed 读到，所以 press → 等 2 帧 → release
	Input.action_press("vehicle")
	await _settle(2)
	Input.action_release("vehicle")
	await _settle(3)
	return bool(_box.call("get", "InVehicle")) == on

func _initialize() -> void:
	var node: Node = load("res://jungle.tscn").instantiate()
	root.add_child(node)
	await process_frame
	_box = node.get_node("box")
	_zones = [node.get_node("MudZone"), node.get_node("MudZone2")]

	var zone: Area2D = _zones[0]
	print("MudZone 位置=", zone.global_position, "   SpeedMult=", zone.get("SpeedMult"),
		"  JumpMult=", zone.get("JumpMult"), "  AccelMult=", zone.get("AccelMult"),
		"  VehicleSpeedMult=", zone.get("VehicleSpeedMult"),
		"  VehicleAccelMult=", zone.get("VehicleAccelMult"))
	print("Box: Speed=", _box.get("Speed"), "  VehicleSpeedMult=", _box.get("VehicleSpeedMult"))

	# 站到泥潭里的地面上
	_box.global_position = Vector2(zone.global_position.x + 32.0, 200.0)
	for i in range(240):
		await physics_frame
		if _box.is_on_floor():
			break
	await _settle(10)
	print("落定位置=", _box.global_position.snapped(Vector2(0.1, 0.1)), " 在地面=", _box.is_on_floor())

	print("--- 走路 ---")
	_set_mud(false)
	print("  ① 无泥走路"); var walk_dry: float = await _speed()
	_set_mud(true)
	print("  ② 泥潭走路"); var walk_mud: float = await _speed()

	print("--- 上车 ---")
	_box.call("GiveVehicle", _box.get_node("Sprite2D").texture, 4, 0.7)
	print("  上车成功=", await _ride(true))
	_set_mud(false)
	print("  ③ 无泥开车"); var ride_dry: float = await _speed()
	_set_mud(true)
	print("  ④ 泥潭开车 ← 本次改动"); var ride_mud: float = await _speed()

	print("--- 下车 ---")
	print("  下车成功=", await _ride(false))
	print("  ⑤ 下车后泥潭走路"); var walk_mud2: float = await _speed()

	print("=== 汇总（像素/秒）===")
	print("无泥走路 %.1f | 泥潭走路 %.1f (×%.2f)" % [walk_dry, walk_mud, walk_mud / walk_dry])
	print("无泥开车 %.1f | 泥潭开车 %.1f (×%.2f)" % [ride_dry, ride_mud, ride_mud / ride_dry])
	print("泥潭开车 ÷ 泥潭走路 = %.2f（< 1 表示开车比走路还慢）" % (ride_mud / walk_mud))
	print("下车后恢复：%.1f" % walk_mud2)
	quit()

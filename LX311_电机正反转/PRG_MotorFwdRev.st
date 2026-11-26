(*=============================================================
  项目    : LX311 三相异步电机正反转控制
  控制器  : 和利时 LX-CU311（16DI / 16DO，DO 为 NPN 型 24VDC 0.5A）
  软件    : iAT（Intelligent AutoThink）V5.0，IEC 61131-3
  语言    : ST
  -------------------------------------------------------------
  I/O 分配
    DI0  正转启动 SB1 (NO)      DO0  正转接触器 KM1
    DI1  反转启动 SB2 (NO)      DO1  反转接触器 KM2
    DI2  停止     SB3 (NC)      DO6  运行指示灯 HL1
    DI3  热继电器 FR  (NC)      DO7  故障指示灯 HL2
    DI4  急停     SB0 (NC)
    DI5  故障复位 SB4 (NO)
  -------------------------------------------------------------
  要点
    1) KM1/KM2 软件互锁 + 外部辅助触点硬件互锁，双重保险
    2) 换向先断后合，死区 300 ms（改 tonDead 的 PT 即可）
    3) 热继、急停 → 故障锁存，必须按复位才能再启动
    4) 停止/急停/热继全部按常闭接入：断线即停机（故障安全）
=============================================================*)
PROGRAM PRG_MotorFwdRev
VAR
    (* ---------------- 输入 ---------------- *)
    xFwdPB     : BOOL := FALSE;  (* DI0 正转启动，NO *)
    xRevPB     : BOOL := FALSE;  (* DI1 反转启动，NO *)
    xStopPB    : BOOL := TRUE;   (* DI2 停止，NC：未按 = TRUE *)
    xFR        : BOOL := TRUE;   (* DI3 热继电器，NC：正常 = TRUE *)
    xEStop     : BOOL := TRUE;   (* DI4 急停，NC：正常 = TRUE *)
    xResetPB   : BOOL := FALSE;  (* DI5 故障复位，NO *)

    (* ---------------- 输出 ---------------- *)
    xKM1       : BOOL;           (* DO0 正转接触器 *)
    xKM2       : BOOL;           (* DO1 反转接触器 *)
    xLampRun   : BOOL;           (* DO6 运行指示 *)
    xLampFault : BOOL;           (* DO7 故障指示 *)

    (* ---------------- 内部 ---------------- *)
    xFault     : BOOL;           (* 故障锁存 *)
    xFwdReq    : BOOL;           (* 正转请求，自锁 *)
    xRevReq    : BOOL;           (* 反转请求，自锁 *)
    xFwdOld    : BOOL;           (* 上一扫描周期按钮值，用于取上升沿 *)
    xRevOld    : BOOL;
    xFwdEdge   : BOOL;
    xRevEdge   : BOOL;
    tonDead    : TON;            (* 换向死区定时器 *)
    iStep      : INT;            (* 0 = 停止，1 = 正转，2 = 反转 *)
END_VAR

(* ---------- 1. 故障锁存 ---------- *)
(* 热继或急停任一路断开 → 置故障；热继/急停恢复且按复位 → 清故障 *)
IF (NOT xFR) OR (NOT xEStop) THEN
    xFault := TRUE;
ELSIF xResetPB AND xFR AND xEStop THEN
    xFault := FALSE;
END_IF;

(* ---------- 2. 按钮上升沿（不依赖库，只用 BOOL） ---------- *)
xFwdEdge := xFwdPB AND NOT xFwdOld;
xRevEdge := xRevPB AND NOT xRevOld;
xFwdOld  := xFwdPB;
xRevOld  := xRevPB;

(* ---------- 3. 方向请求：后按优先 ---------- *)
IF xFwdEdge AND NOT xFault THEN
    xFwdReq := TRUE;
    xRevReq := FALSE;
END_IF;

IF xRevEdge AND NOT xFault THEN
    xRevReq := TRUE;
    xFwdReq := FALSE;
END_IF;

(* 停止或故障 → 清除方向请求 *)
IF (NOT xStopPB) OR xFault THEN
    xFwdReq := FALSE;
    xRevReq := FALSE;
END_IF;

(* ---------- 4. 换向死区：输出全断后开始计时 ---------- *)
tonDead(IN := (iStep = 0), PT := T#300MS);

(* ---------- 5. 方向状态机 ---------- *)
CASE iStep OF
0:  (* 停止：死区到且无故障才允许启动 *)
    IF xStopPB AND NOT xFault AND tonDead.Q THEN
        IF xFwdReq THEN
            iStep := 1;
        ELSIF xRevReq THEN
            iStep := 2;
        END_IF;
    END_IF;

1:  (* 正转运行：停止 / 故障 / 有反转请求 → 先回停止 *)
    IF (NOT xStopPB) OR xFault OR xRevReq THEN
        iStep := 0;
    END_IF;

2:  (* 反转运行 *)
    IF (NOT xStopPB) OR xFault OR xFwdReq THEN
        iStep := 0;
    END_IF;

ELSE
    iStep := 0;
END_CASE;

(* ---------- 6. 输出映射（同一 iStep 派生，天然互斥） ---------- *)
xKM1 := (iStep = 1);
xKM2 := (iStep = 2);

xLampRun   := (iStep <> 0);
xLampFault := xFault;

END_PROGRAM

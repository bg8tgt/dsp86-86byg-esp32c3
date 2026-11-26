(*=============================================================
  项目    : LX311 电机正反转控制（带威纶通 HMI 版）
  控制器  : 和利时 LX-CU311（16DI/16DO，DO 为 NPN 型 24VDC）
  HMI     : 威纶通 MT8071iE（800×480），Modbus TCP 主站
  -------------------------------------------------------------
  相比基础版的改动
    1) 增加 HMI 命令位：正转/反转/停止/复位
    2) 停止信号合成：本地停止 OR HMI 停止
    3) 增加运行计时、累计运行、启动次数、故障代码（供 HMI 显示）
    4) HMI 命令与本地按钮同等对待，均走"上升沿 + 状态机"
  -------------------------------------------------------------
  注意：HMI 上的"停止"不能替代硬件急停，急停必须保留硬线回路
=============================================================*)
PROGRAM PRG_MotorFwdRev_HMI
VAR
    (* ---------------- 本地输入 ---------------- *)
    xFwdPB     : BOOL := FALSE;  (* DI0 正转启动，NO *)
    xRevPB     : BOOL := FALSE;  (* DI1 反转启动，NO *)
    xStopPB    : BOOL := TRUE;   (* DI2 停止，NC：未按 = TRUE *)
    xFR        : BOOL := TRUE;   (* DI3 热继电器，NC：正常 = TRUE *)
    xEStop     : BOOL := TRUE;   (* DI4 急停，NC：正常 = TRUE *)
    xResetPB   : BOOL := FALSE;  (* DI5 故障复位，NO *)

    (* ---------------- HMI 命令（Modbus 线圈） ---------------- *)
    xHmiFwd    : BOOL;           (* 0x0000 正转启动，复归型 *)
    xHmiRev    : BOOL;           (* 0x0001 反转启动 *)
    xHmiStop   : BOOL;           (* 0x0002 停止 *)
    xHmiReset  : BOOL;           (* 0x0003 故障复位 *)

    (* ---------------- 输出 ---------------- *)
    xKM1       : BOOL;           (* DO0 正转接触器 *)
    xKM2       : BOOL;           (* DO1 反转接触器 *)
    xLampRun   : BOOL;           (* DO6 运行指示 *)
    xLampFault : BOOL;           (* DO7 故障指示 *)

    (* ---------------- 内部 ---------------- *)
    xFault     : BOOL;
    xFwdReq    : BOOL;
    xRevReq    : BOOL;
    xFwdOld    : BOOL;
    xRevOld    : BOOL;
    xHmiFwdOld : BOOL;
    xHmiRevOld : BOOL;
    xFwdEdge   : BOOL;
    xRevEdge   : BOOL;
    xStopEff   : BOOL;           (* 本地停止 OR HMI 停止 *)
    tonDead    : TON;            (* 换向死区 300 ms *)
    tonPulse   : TON;            (* 1 s 秒脉冲 *)
    iStep      : INT;            (* 0 停止 1 正转 2 反转 *)
    iStepOld   : INT;

    (* ---------------- 供 HMI 读取 ---------------- *)
    iRunSec      : INT;          (* 4x0000 本次运行秒 *)
    iStartCnt    : INT;          (* 4x0001 累计启动次数 *)
    iFaultCode   : INT;          (* 4x0002 0 无 1 热继 2 急停 *)
    iRunTotalMin : INT;          (* 4x0004 累计运行分钟 *)
    xRunFwd      : BOOL;         (* 0x0010 *)
    xRunRev      : BOOL;         (* 0x0011 *)
    xRunAny      : BOOL;         (* 0x0015 *)
END_VAR

(* ---------- 1. 故障锁存 ---------- *)
IF (NOT xFR) OR (NOT xEStop) THEN
    xFault := TRUE;
ELSIF (xResetPB OR xHmiReset) AND xFR AND xEStop THEN
    xFault := FALSE;
END_IF;

(* 故障代码 */
IF NOT xFR THEN
    iFaultCode := 1;
ELSIF NOT xEStop THEN
    iFaultCode := 2;
ELSE
    iFaultCode := 0;
END_IF;

(* ---------- 2. 停止合成 ---------- *)
xStopEff := xStopPB AND NOT xHmiStop;

(* ---------- 3. 按钮上升沿（本地 + HMI） ---------- *)
xFwdEdge := (xFwdPB AND NOT xFwdOld) OR (xHmiFwd AND NOT xHmiFwdOld);
xRevEdge := (xRevPB AND NOT xRevOld) OR (xHmiRev AND NOT xHmiRevOld);
xFwdOld   := xFwdPB;
xRevOld   := xRevPB;
xHmiFwdOld := xHmiFwd;
xHmiRevOld := xHmiRev;

(* ---------- 4. 方向请求：后按优先 ---------- *)
IF xFwdEdge AND NOT xFault THEN
    xFwdReq := TRUE;
    xRevReq := FALSE;
END_IF;

IF xRevEdge AND NOT xFault THEN
    xRevReq := TRUE;
    xFwdReq := FALSE;
END_IF;

IF (NOT xStopEff) OR xFault THEN
    xFwdReq := FALSE;
    xRevReq := FALSE;
END_IF;

(* ---------- 5. 换向死区 ---------- *)
tonDead(IN := (iStep = 0), PT := T#300MS);

(* ---------- 6. 方向状态机 ---------- *)
CASE iStep OF
0:
    IF xStopEff AND NOT xFault AND tonDead.Q THEN
        IF xFwdReq THEN
            iStep := 1;
        ELSIF xRevReq THEN
            iStep := 2;
        END_IF;
    END_IF;

1:
    IF (NOT xStopEff) OR xFault OR xRevReq THEN
        iStep := 0;
    END_IF;

2:
    IF (NOT xStopEff) OR xFault OR xFwdReq THEN
        iStep := 0;
    END_IF;

ELSE
    iStep := 0;
END_CASE;

(* ---------- 7. 统计（HMI 显示） ---------- *)
IF (iStep <> 0) AND (iStepOld = 0) THEN
    iStartCnt := iStartCnt + 1;
    iRunSec   := 0;
END_IF;
iStepOld := iStep;

tonPulse(IN := NOT tonPulse.Q, PT := T#1S);
IF tonPulse.Q AND (iStep <> 0) THEN
    iRunSec := iRunSec + 1;
    IF (iRunSec MOD 60) = 0 THEN
        iRunTotalMin := iRunTotalMin + 1;
    END_IF;
END_IF;

(* ---------- 8. 输出映射 ---------- *)
xKM1 := (iStep = 1);
xKM2 := (iStep = 2);
xLampRun   := (iStep <> 0);
xLampFault := xFault;

xRunFwd := (iStep = 1);
xRunRev := (iStep = 2);
xRunAny := (iStep <> 0);

END_PROGRAM

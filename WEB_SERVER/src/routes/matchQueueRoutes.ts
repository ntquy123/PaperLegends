import express from "express";
import {
  joinQueue,
  cancelQueue,
  forceStartQueue,
  matchReady,
  matchStarted,
  matchResult,
  matchEarlyExit,
  dsRegister,
  releaseServerPort,
  getOnlineCount,
} from "../controllers/matchQueueController";

const router = express.Router();

// Client actions
router.post("/queue/join", joinQueue);
router.post("/queue/cancel", cancelQueue);
router.post("/queue/force-start", forceStartQueue);
router.post("/match/early-exit", matchEarlyExit);

// Dedicated server callbacks
router.post("/internal/match/ready", matchReady);
router.post("/internal/match/started", matchStarted);
router.post("/internal/match/result", matchResult);

// Warm pool: DS idle đăng ký về backend
router.post("/internal/ds/register", dsRegister);
router.post("/internal/server/port/release", releaseServerPort);
router.get("/internal/online-count", getOnlineCount);

export default router;

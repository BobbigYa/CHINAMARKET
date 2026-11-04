"""Inspect the isolated CI database without printing its credential hash."""

from __future__ import annotations

import argparse
import base64
import json
import sqlite3
from pathlib import Path

TEST_PASSWORD = b"UiTest-Password-2026!"
TEST_WRONG_PASSWORD = b"UiTest-Wrong-Password!"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("database", type=Path)
    args = parser.parse_args()
    path = args.database.resolve()

    connection = sqlite3.connect(f"file:{path.as_posix()}?mode=ro", uri=True)
    try:
        row = connection.execute(
            "SELECT PasswordHash, FailedAttempts, LockoutUntilUtc "
            "FROM AdministratorCredential WHERE Id = 1"
        ).fetchone()
    finally:
        connection.close()

    assert row is not None, "管理员凭据记录不存在"
    encoded, failed_attempts, lockout_until = row
    parts = encoded.split("$")
    assert len(parts) == 6, "Argon2id 记录段数不正确"
    assert parts[1] == "argon2id", "算法不是 Argon2id"
    assert parts[2] == "v=19", "Argon2 版本不是 19"
    assert parts[3] == "m=65536,t=3,p=1", f"生产参数被改变：{parts[3]}"
    salt = base64.b64decode(parts[4], validate=True)
    digest = base64.b64decode(parts[5], validate=True)
    assert len(salt) == 16, "盐长度不是 16 字节"
    assert len(digest) == 32, "哈希长度不是 32 字节"
    assert failed_attempts == 0, "成功登录后失败计数未清零"
    assert lockout_until is None, "成功登录后仍存在锁定状态"

    sensitive_files = [path]
    sensitive_files.extend(
        candidate for candidate in (Path(f"{path}-wal"), Path(f"{path}-shm"))
        if candidate.exists()
    )
    for candidate in sensitive_files:
        raw = candidate.read_bytes()
        assert TEST_PASSWORD not in raw, f"{candidate.name} 中发现测试密码明文"
        assert TEST_WRONG_PASSWORD not in raw, f"{candidate.name} 中发现错误测试密码明文"

    log_files = list(path.parent.rglob("*.log"))
    assert not log_files, f"发现未预期的日志文件：{log_files}"

    evidence = {
        "databasePath": str(path),
        "algorithm": "argon2id",
        "version": 19,
        "memoryKiB": 65536,
        "iterations": 3,
        "parallelism": 1,
        "saltBytes": len(salt),
        "hashBytes": len(digest),
        "plaintextTestPasswordFound": False,
        "unexpectedLogFiles": 0,
        "failedAttemptsAfterSuccess": failed_attempts,
    }
    output = Path("artifacts/database-inspection.json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        "::notice title=TPOS 生产数据库检查::"
        "Argon2id m=65536,t=3,p=1；16 字节盐；32 字节哈希；"
        "未发现测试密码明文或敏感日志。"
    )


if __name__ == "__main__":
    main()

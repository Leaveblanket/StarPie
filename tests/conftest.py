import os
import subprocess
import pytest
from pywinauto import Application, Desktop

# -OnScreen 调试形态（窗口可见、Save 的系统提示框真实弹出）。
# 默认（静默后台形态）被测应用离屏且提示框不呈现（ADR-0031），e2e 不应等待任何弹窗。
ONSCREEN = os.environ.get("STARPIE_E2E_ONSCREEN") == "1"


def dismiss_messagebox(timeout: float = 3.0) -> None:
    """关闭 Save 触发的系统提示框（#32770）。

    仅 -OnScreen 调试形态会有提示框；静默后台形态下 DialogService 直接不呈现，
    立即返回，避免每个用例白等 timeout（ADR-0031）。
    """
    if not ONSCREEN:
        return
    try:
        dialog = Desktop(backend="uia").window(class_name="#32770")
        if dialog.exists(timeout=timeout):
            dialog.child_window(control_type="Button").invoke()
    except Exception:
        pass


@pytest.fixture(scope="function")
def sandbox_env(tmp_path):
    """
    Sets up isolated AppData folder structures to prevent config pollution.
    """
    env = os.environ.copy()
    local_app_data = tmp_path / "AppData" / "Local"
    app_data = tmp_path / "AppData" / "Roaming"
    temp_dir = tmp_path / "Temp"
    
    for path in (local_app_data, app_data, temp_dir):
        path.mkdir(parents=True, exist_ok=True)
        
    env["LOCALAPPDATA"] = str(local_app_data)
    env["APPDATA"] = str(app_data)
    env["TEMP"] = str(temp_dir)
    env["TMP"] = str(temp_dir)
    
    return env, local_app_data

@pytest.fixture(scope="function")
def app(sandbox_env, request):
    env, local_app_data = sandbox_env
    
    # Locate the executable
    project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    candidates = [
        os.path.join(project_root, "StarPie", "bin", config, tfm, "StarPie.exe")
        for config in ("Release", "Debug")
        for tfm in ("net10.0-windows10.0.19041.0", "net10.0-windows")
    ]
    app_path = next((c for c in candidates if os.path.exists(c)), None)
    if not app_path:
        pytest.fail(f"Executable not found in {candidates}. Please build the project first.")
        
    # Start the process with sandboxed environment variables.
    # 默认静默后台形态（--background：离屏 + 不可激活 + 不进任务栏/托盘，不打扰同机用户）；
    # ONSCREEN（STARPIE_E2E_ONSCREEN=1，scripts/run-e2e.ps1 -OnScreen）时窗口正常显示，供调试。
    flags = ["--allow-multiple"]
    if not ONSCREEN:
        flags.append("--background")
    proc = subprocess.Popen([app_path, *flags], env=env)
    
    # Connect pywinauto using PID; poll for readiness instead of a fixed sleep
    try:
        pw_app = Application(backend="uia").connect(process=proc.pid, timeout=15)
        win = pw_app.window(title_re="StarPie.*")
        win.wait("visible", timeout=15)
    except Exception as ex:
        proc.terminate()
        pytest.fail(f"Failed to launch or connect to application window: {ex}")
        
    yield win, local_app_data
    
    # Screenshot on failure
    if getattr(getattr(request.node, "rep_call", None), "failed", False):
        artifacts_dir = os.path.join(project_root, "artifacts")
        os.makedirs(artifacts_dir, exist_ok=True)
        try:
            img = win.capture_as_image()
            if img is None:
                # capture_as_image 需要 PIL；venv 未装时返回 None（既有环境缺口，见 ADR-0031）
                print("失败截图不可用：capture_as_image 返回 None（e2e venv 未安装 pillow）")
            else:
                path = os.path.join(artifacts_dir, f"FAIL_{request.node.name}.png")
                img.save(path)
                print(f"失败截图已保存：{path}")
        except Exception as ex:
            print(f"失败截图异常：{type(ex).__name__}: {ex}")
            
    # Clean shutdown
    try:
        proc.kill()
        proc.wait(timeout=2)
    except Exception:
        pass

@pytest.hookimpl(tryfirst=True, hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    setattr(item, f"rep_{outcome.get_result().when}", outcome.get_result())

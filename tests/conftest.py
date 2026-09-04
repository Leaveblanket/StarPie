import os
import subprocess
import pytest
import time
from pywinauto import Application

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
        os.path.join(project_root, "WinPieGestures", "bin", "Release", "net8.0-windows10.0.19041.0", "StarPie.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Release", "net8.0-windows10.0.19041.0", "WinPieGestures.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Debug", "net8.0-windows10.0.19041.0", "StarPie.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Debug", "net8.0-windows10.0.19041.0", "WinPieGestures.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Release", "net8.0-windows", "StarPie.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Release", "net8.0-windows", "WinPieGestures.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Debug", "net8.0-windows", "StarPie.exe"),
        os.path.join(project_root, "WinPieGestures", "bin", "Debug", "net8.0-windows", "WinPieGestures.exe"),
    ]
    app_path = next((c for c in candidates if os.path.exists(c)), None)
    if not app_path:
        pytest.fail(f"Executable not found in {candidates}. Please build the project first.")
        
    # Start the process with sandboxed environment variables
    proc = subprocess.Popen([app_path, "--allow-multiple"], env=env)
    
    # Connect pywinauto using PID
    time.sleep(1.5)
    try:
        pw_app = Application(backend="uia").connect(process=proc.pid, timeout=10)
        win = pw_app.window(title_re="(StarPie|WinPieGestures).*")
        win.wait("visible", timeout=10)
    except Exception as ex:
        proc.terminate()
        pytest.fail(f"Failed to launch or connect to application window: {ex}")
        
    yield win, local_app_data
    
    # Screenshot on failure
    if getattr(getattr(request.node, "rep_call", None), "failed", False):
        artifacts_dir = os.path.join(project_root, "artifacts")
        os.makedirs(artifacts_dir, exist_ok=True)
        try:
            win.capture_as_image().save(
                os.path.join(artifacts_dir, f"FAIL_{request.node.name}.png")
            )
        except Exception:
            pass
            
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

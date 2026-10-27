# Greenhouse Digital Twin (Unity)

A Unity 6 project that builds a 3D model of two small glasshouses from one configuration file and shows live sensor readings and device states on the model. It runs without any hardware: a built-in simulated source produces data, and a TCP interface accepts a live feed from your own data system.

## What it does

- **Parametric model.** The frame, glazing, sliding door, roof vents, louvre, pots and plants, heater, LED bars, outdoor control box, the tree row to the north and the spacing between the two houses all come from a single config asset. Change a value and the model rebuilds.
- **Live data on the model.** Each sensor has a marker and a label showing every numeric value it reports (temperature, humidity, VPD, CO2, PAR, energy, power, and any new field). A reading older than 300 s turns grey. The heater glows when on, the LED bars light up, the vents open to their reported angle, and the door slides open.
- **Two panels.** The left panel holds the connection, geometry, vents, pots and LEDs, sensors, manual device buttons and config import/export. The right panel shows, for each house, the average temperature and humidity, device states, and a chart of recent history with a 2-hour forecast.
- **Real sun angle.** The directional light follows the sun for a given latitude, longitude, date and time, using NOAA's [General Solar Position Calculations](https://gml.noaa.gov/grad/solcalc/solareqns.PDF), so shadows match the season. The default is noon on the winter solstice.
- **Manual commands.** On/Off and Open/Close buttons send a command line back over the same TCP connection.

## Requirements

- Unity **6000.0.85f1** (Unity 6 LTS) with the built-in render pipeline. Other 6000.0.x versions have not been tested.
- Newtonsoft Json 3.2.2 (`com.unity.nuget.newtonsoft-json`), which ships with the editor.
- The interface is IMGUI only; there is no uGUI or TextMeshPro dependency.

## Getting started

1. Install Unity Hub and editor 6000.0.85f1.
2. Clone this repository. In Unity Hub choose **Projects → Add → Add project from disk** and select the `SolarGrowTwin` folder. The first import takes a few minutes.
3. In the editor menu choose **SolarGrow → Create Twin Scene**. This creates `Assets/SolarGrow/Scenes/Twin.unity` and `Assets/SolarGrow/Config/GreenhouseConfig.asset`. Both are generated on each machine and are not tracked in git.
4. Press **Play**. The default data source is *Simulated*: each house gets a new frame every 2 s, and the heater, vents, lights and pump switch by simple rules.

Pressing Play in an empty scene also builds the whole twin (`TwinBootstrap`).

## Controls (Play mode)

| Input | Action |
|---|---|
| Left-drag | Orbit |
| Right- or middle-drag | Pan |
| Mouse wheel | Zoom |
| `1`, `2` | Focus house A or B |
| `F` | Back to the starting view |

## Configuration

Select `Assets/SolarGrow/Config/GreenhouseConfig.asset` in the Project window. Every field has a tooltip. In edit mode the model rebuilds a moment after a change; **SolarGrow → Rebuild Greenhouses** forces it. In Play mode the left panel edits the same asset, and the values stay after you stop. **Export JSON** and **Import JSON** save and load the whole configuration.

Coordinates are in metres in each house's own axes: x along the ridge, y up, z towards the north wall, with the origin at the centre of the floor.

The default layout:

- The glasshouse is 1976 × 2632 mm, with eaves at 1691 mm and the ridge at 2249 mm.
- The sliding door is on the east gable, facing house B, and the louvre is on the west gable.
- The two roof vents are staggered, one on each slope, in the middle two of the four bays between the rafters. The vent on the north slope is the one nearer the door.
- The control box and energy meter stand on the ground outside the door end.
- The heater sits in the aisle at the far end from the door.
- A row of trees runs along the north side, about twice the height of the house.
- House B is 1.5 house lengths east of house A.

Sizes that are not on the manufacturer's drawing are estimates, among them the heater, LED bars, control box and the tree row.

### Adding sensors and devices

- **Sensor:** add an entry under *Sensors* in the config asset, or use *Add sensor* in the left panel. Its `id` must match a key under `sensors` in the incoming frame. A sensor id that is not in the config still gets a marker, placed in a row above the ridge, so no reading is lost.
- **Device:** add an entry under *Actuators*. `deviceName` is the key under `devices` in the frame, `kind` decides the buttons, and `componentId` is the model part that shows it (`heater`, `led`, `vent-1`, `vent-2`, `door`, `pump`). A new kind of 3D part needs a few lines in `GreenhouseBuilder.Parts.cs`, where each part is built by its own function.

## Data interface (protocol version 1)

Unity is a TCP client. Your data bridge is the server, by default at `127.0.0.1:5055`. Every message is one UTF-8 JSON object on one line ending in `\n`. In the left panel choose **TCP JSON lines**, enter the host and port, then **Connect**.

**Bridge → Unity**

- On connect: `{"type":"hello","bridge":"<name>","version":1,"greenhouses":["A","B"]}`
- For each new state: `{"type":"state", ...frame...}`
- Reply to a ping: `{"type":"pong"}`

A frame can carry:

| Field | Content |
|---|---|
| `greenhouse` | house id, e.g. `"A"` |
| `updated_at_utc` | ISO 8601 time |
| `sensors` | `{id: {observed_at_utc, temperature_c, relative_humidity_percent, co2_ppm, par_umol_m2_s, energy_kwh, power_w, ...}}` |
| `mean` | `{sensor_count, observed_at_utc, temperature_c, relative_humidity_percent, temperature_spread_c}` |
| `devices` | `{name: "on" \| "off" \| "open" \| "closed" \| 0–1}`, the state the controller asked for |
| `nodes` | `{node_id: {..., reported_actuators: {...}}}`, the state the hardware reports; it overrides `devices` |
| `contacts` | `{name: "open" \| "closed"}`, e.g. `door` |
| `twin` | optional; `forecast.points` is a list of `{at_utc, temperature_c, relative_humidity_percent}` |

Unknown fields are ignored and missing fields are tolerated.

**Unity → bridge**

- Manual command: `{"type":"cmd","greenhouse":"A","device":"heater","value":"on","source":"unity-manual","seq":12,"sent_at_utc":"..."}`
- Keep-alive: `{"type":"ping"}` every 10 s

If nothing arrives for 35 s, Unity treats the link as dead and reconnects, waiting 1, 2, 4 and so on up to 15 s between attempts.

**Minimal test server** (Python 3.8+, standard library only):

```python
import json, socket, time
from datetime import datetime, timezone

server = socket.create_server(("127.0.0.1", 5055))
conn, _ = server.accept()
send = lambda obj: conn.sendall((json.dumps(obj) + "\n").encode())
send({"type": "hello", "bridge": "example", "version": 1, "greenhouses": ["A"]})
while True:
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    send({"type": "state", "greenhouse": "A", "updated_at_utc": now,
          "sensors": {"th-07": {"observed_at_utc": now, "temperature_c": 18.5,
                                "relative_humidity_percent": 72}},
          "devices": {"heater": "on", "vent": 0.3}})
    time.sleep(2)
```

## Project layout

```
SolarGrowTwin/Assets/SolarGrow/
  Scripts/Config/   GreenhouseConfig (every parameter, defaults, JSON import/export), sensor and actuator specs
  Scripts/Model/    GreenhouseBuilder (builds the house from primitives), animated parts, glass meshes
  Scripts/Data/     frame wrapper, source interface, TCP client, simulated source, history buffer
  Scripts/Runtime/  TwinController (routes frames to the model), camera, scene setup
  Scripts/UI/       left and right panels, history and forecast chart
  Scripts/Util/     materials, labels, JSON helpers, sun position
  Editor/           SolarGrow menu: Create Twin Scene, Create Default Config, Rebuild Greenhouses
```

## Limitations

- Unity only displays the data and sends manual commands. It contains no control logic.
- Labels can overlap when many are shown; switch them off in the left panel.
- A standalone build has not been tested. For a build, add the Standard shader to *Graphics → Always Included Shaders*.

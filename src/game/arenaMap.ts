export type ArenaMapData = {
  width: number;
  height: number;
  data: Uint8ClampedArray;
  worldScale?: number;
};

export function mapPixel(map: ArenaMapData | undefined, x: number, y: number): 'walkable' | 'collision' | 'fall' {
  if (!map) return 'walkable';
  const scale = map.worldScale || 1;
  const mapX = x / scale;
  const mapY = y / scale;
  if (mapX < 0 || mapY < 0 || mapX >= map.width || mapY >= map.height) return 'fall';
  const ix = (Math.floor(mapY) * map.width + Math.floor(mapX)) * 4;
  const r = map.data[ix];
  const g = map.data[ix + 1];
  const b = map.data[ix + 2];
  if (g > 200 && r < 80 && b < 80) return 'walkable';
  if (r > 200 && g < 80 && b < 80) return 'collision';
  return 'fall';
}

export function playerMapPixel(map: ArenaMapData | undefined, x: number, y: number, radius: number) {
  const samples = [[x, y], [x - radius, y], [x + radius, y], [x, y - radius], [x, y + radius]];
  return samples.map(([sx, sy]) => mapPixel(map, sx, sy));
}

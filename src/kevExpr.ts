import {PointsVec} from "./char";
import {Point} from "./PointDisplay";

const tokenPat = /[kev+-]|(\d+)/gi;
export function parse(text: string, untyped: Point): PointsVec {
  const points = {channeled: 0, exhausted: 0, consumed: 0};
  let factor = 1;
  let type = untyped;

  for (const token of text.matchAll(tokenPat)) {
    switch (token[0]) {
      case '+':
        factor = 1;
        break;
      case '-':
        factor = -1;
        break;
      case "k":
      case "K":
        type = Point.Channeled;
        break;
      case "e":
      case "E":
        type = Point.Exhausted;
        break;
        case "v":
      case "V":
        type = Point.Consumed;
        break;
      default:
        let value = parseInt(token[0]);
        switch (type) {
          case Point.Channeled:
            points.channeled += value * factor;
            break;
          case Point.Exhausted:
            points.exhausted += value * factor;
            break;
          case Point.Consumed:
            points.consumed += value * factor;
            break;
        }
        break;
    }
  }
  return points;
}

export function render(points: PointsVec): string {
  let text = [];
  if(points.channeled > 0) {
    text.push('K');
    text.push(points.channeled);
  }
  if(points.exhausted > 0) {
    text.push('E');
    text.push(points.exhausted);
  }
  if(points.consumed > 0) {
    text.push('V');
    text.push(points.consumed);
  }
  if(points.channeled < 0 || points.exhausted < 0 || points.consumed < 0) {
    text.push('-');
    if(points.channeled < 0) {
      text.push('K');
      text.push(-points.channeled);
    }
    if(points.exhausted < 0) {
      text.push('E');
      text.push(-points.exhausted);
    }
    if(points.consumed < 0) {
      text.push('V');
      text.push(-points.consumed);
    }
  }
  return text.join('');
}

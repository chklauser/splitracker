import {FunctionComponent} from "react";
import {classSet} from "../ClassSet";
import "./PointDisplay.scss";

export const enum Point {
  Channeled = '∕',
  Exhausted = '⨉',
  Consumed = '※',
  Free = ' '
}

export interface IPointDisplayProps {
  point: Point,
  value: 1 | 0 | -1,
  highlightDelta?: boolean
}

function iconFor(point: Point) {
  switch (point) {
    case Point.Channeled:
      return "/K.svg";
    case Point.Exhausted:
      return "/E.svg";
    case Point.Consumed:
      return "/V.svg";
    default:
      throw Error("Invalid point: " + point);
  }
}

export const PointDisplay: FunctionComponent<IPointDisplayProps> = ({point, value, highlightDelta = false}) => {

  const pointClasses = classSet({
    PointDisplay: true,
    "PointDisplay-heal": highlightDelta && value < 0,
    "PointDisplay-harm": highlightDelta && value > 0,
    "PointDisplay-free": value == 0,
    "PointDisplay-channeled": point === Point.Channeled,
    "PointDisplay-exhausted": point === Point.Exhausted,
    "PointDisplay-consumed": point === Point.Consumed,
  });
  if (point == Point.Free) {
    return <div className={pointClasses}/>;
  } else {
    return <img className={pointClasses} src={iconFor(point)} alt={point}/>
  }
};
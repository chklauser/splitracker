import "./drag.css"
import {PointsVec} from "./char";

export const enum ItemTypes {
  PointsPreview = 'PointsPreview'
}

export interface PointsPreviewData {
  points: PointsVec;
  channelingIndex?: number;
}
